using System.Text.Json;
using Hackathon.Application.InterviewSessions;
using Hackathon.Application.InterviewTasks;
using Hackathon.Application.VoiceAgent;
using Hackathon.Domain.InterviewSessions;
using Hackathon.Domain.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewOverview;

public sealed class BuildInterviewOverviewCommandHandler(
    IInterviewSessionStore sessionStore,
    IInterviewConversationStore conversationStore,
    IPendingInterviewTaskStore pendingTasks,
    IInteractiveAnswerReader answerReader,
    IElevenLabsTranscriptClient transcriptClient,
    IInteractiveSolutionJudge solutionJudge,
    IInterviewOverviewArtifactStore artifactStore,
    IInterviewOverviewComposer overviewComposer)
    : IRequestHandler<BuildInterviewOverviewCommand, BuildInterviewOverviewResult>
{
    public async Task<BuildInterviewOverviewResult> Handle(
        BuildInterviewOverviewCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        try
        {
            var session = await sessionStore.GetAsync(
                request.UserId,
                request.SessionId,
                cancellationToken)
                ?? throw new InvalidOperationException("Interview session not found.");

            var conversations = await conversationStore.ListBySessionAsync(
                request.SessionId,
                cancellationToken);

            var turns = new List<TranscriptTurn>();
            foreach (var conversation in conversations)
            {
                if (string.IsNullOrWhiteSpace(conversation.ConversationId))
                {
                    continue;
                }

                try
                {
                    var part = await transcriptClient.GetTranscriptAsync(
                        conversation.ConversationId,
                        cancellationToken);
                    turns.AddRange(part);
                }
                catch
                {
                    // Skip a leg that never produced a transcript; continue with others.
                }
            }

            var transcriptJson = JsonSerializer.Serialize(
                turns.Select(turn => new { role = turn.Role, content = turn.Content }));
            await artifactStore.PutTranscriptAsync(
                request.UserId,
                request.SessionId,
                transcriptJson,
                cancellationToken);

            var tasks = await pendingTasks.ListBySessionAsync(request.SessionId, cancellationToken);
            var judgments = new List<InteractiveJudgment>();

            foreach (var task in tasks.Where(task => task.Status == InteractiveTaskStatus.Answered))
            {
                if (!ShouldJudge(task))
                {
                    continue;
                }

                var judgment = await JudgeTaskAsync(
                    request.UserId,
                    request.SessionId,
                    task,
                    cancellationToken);
                if (judgment is null)
                {
                    continue;
                }

                await artifactStore.PutJudgmentAsync(
                    request.UserId,
                    request.SessionId,
                    task.TaskId,
                    judgment,
                    cancellationToken);
                judgments.Add(judgment);
            }

            var overview = await overviewComposer.ComposeAsync(
                transcriptJson,
                judgments,
                session.Settings.RecruiterLanguage,
                cancellationToken);

            await artifactStore.PutOverviewAsync(
                request.UserId,
                request.SessionId,
                overview,
                cancellationToken);

            await sessionStore.UpdateOverviewStatusAsync(
                request.UserId,
                request.SessionId,
                OverviewStatus.Ready,
                cancellationToken);

            return new BuildInterviewOverviewResult(OverviewStatus.Ready, overview.Score);
        }
        catch
        {
            try
            {
                await sessionStore.UpdateOverviewStatusAsync(
                    request.UserId,
                    request.SessionId,
                    OverviewStatus.Error,
                    cancellationToken);
            }
            catch
            {
                // Prefer original failure.
            }

            throw;
        }
    }

    private static bool ShouldJudge(PendingInterviewTask task) =>
        task.ExpectedInput is ExpectedInput.Code
            or ExpectedInput.Diagram
            or ExpectedInput.Schema
            or ExpectedInput.Text
            or ExpectedInput.FreeString;

    private async Task<InteractiveJudgment?> JudgeTaskAsync(
        string userId,
        string sessionId,
        PendingInterviewTask task,
        CancellationToken cancellationToken)
    {
        if (task.ExpectedInput == ExpectedInput.Code)
        {
            var zip = await TryLoadAnswerAsync(userId, sessionId, task.TaskId, "zip", cancellationToken)
                      ?? await TryLoadAnswerAsync(userId, sessionId, task.TaskId, "bin", cancellationToken);
            if (zip is null)
            {
                return null;
            }

            return await solutionJudge.JudgeCodeAsync(
                zip.Content,
                task.TaskId,
                task.QuestionContentFormatted,
                task.ExpectedInput,
                task.Module,
                cancellationToken);
        }

        InteractiveAnswerObject? visual = null;
        foreach (var extension in new[] { "png", "jpg", "jpeg", "webp", "gif", "pdf" })
        {
            visual = await TryLoadAnswerAsync(userId, sessionId, task.TaskId, extension, cancellationToken);
            if (visual is not null)
            {
                break;
            }
        }

        if (visual is not null &&
            (visual.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
             visual.ContentType is "application/pdf" ||
             task.ExpectedInput is ExpectedInput.Diagram or ExpectedInput.Schema))
        {
            return await solutionJudge.JudgeVisualAsync(
                visual.Content,
                visual.ContentType,
                task.TaskId,
                task.QuestionContentFormatted,
                task.ExpectedInput,
                task.Module,
                cancellationToken);
        }

        var textAnswer = await TryLoadAnswerAsync(userId, sessionId, task.TaskId, "txt", cancellationToken);
        if (textAnswer is null)
        {
            return null;
        }

        var text = System.Text.Encoding.UTF8.GetString(textAnswer.Content);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return await solutionJudge.JudgeTextAsync(
            text,
            task.TaskId,
            task.QuestionContentFormatted,
            task.ExpectedInput,
            task.Module,
            cancellationToken);
    }

    private async Task<InteractiveAnswerObject?> TryLoadAnswerAsync(
        string userId,
        string sessionId,
        string taskId,
        string extension,
        CancellationToken cancellationToken)
    {
        var key = VisionReviewNeeds.BuildAnswerKey(userId, sessionId, taskId, extension);
        return await answerReader.GetByKeyAsync(key, cancellationToken);
    }
}
