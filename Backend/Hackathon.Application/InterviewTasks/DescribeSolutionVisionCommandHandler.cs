using Hackathon.Domain.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed class DescribeSolutionVisionCommandHandler(
    IPendingInterviewTaskStore pendingTasks,
    IInteractiveAnswerReader answerReader,
    ISolutionVisionDescriber visionDescriber)
    : IRequestHandler<DescribeSolutionVisionCommand, DescribeSolutionVisionResult>
{
    private const int MaxDescriptionChars = 8_000;

    public async Task<DescribeSolutionVisionResult> Handle(
        DescribeSolutionVisionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AnswerKey);

        try
        {
            var task = await pendingTasks.GetAsync(
                request.SessionId,
                request.TaskId,
                cancellationToken);

            if (task is null)
            {
                throw new InvalidOperationException(
                    $"Pending task {request.TaskId} was not found for session {request.SessionId}.");
            }

            var answer = await answerReader.GetByKeyAsync(request.AnswerKey, cancellationToken);
            if (answer is null || answer.Content.Length == 0)
            {
                await pendingTasks.SetVisionReviewAsync(
                    request.SessionId,
                    request.TaskId,
                    VisionReviewStatus.Failed,
                    null,
                    cancellationToken);

                return new DescribeSolutionVisionResult(VisionReviewStatus.Failed, 0);
            }

            var taskPrompt =
                task.QuestionContentFormatted?.Trim()
                ?? task.QuestionContent?.Trim()
                ?? string.Empty;

            var description = await visionDescriber.DescribeAsync(
                answer.Content,
                answer.ContentType,
                taskPrompt,
                task.ExpectedInput,
                task.Module,
                cancellationToken);

            description = Truncate(description.Trim(), MaxDescriptionChars);
            if (string.IsNullOrWhiteSpace(description))
            {
                await pendingTasks.SetVisionReviewAsync(
                    request.SessionId,
                    request.TaskId,
                    VisionReviewStatus.Failed,
                    null,
                    cancellationToken);

                return new DescribeSolutionVisionResult(VisionReviewStatus.Failed, 0);
            }

            await pendingTasks.SetVisionReviewAsync(
                request.SessionId,
                request.TaskId,
                VisionReviewStatus.Ready,
                description,
                cancellationToken);

            return new DescribeSolutionVisionResult(VisionReviewStatus.Ready, description.Length);
        }
        catch
        {
            try
            {
                await pendingTasks.SetVisionReviewAsync(
                    request.SessionId,
                    request.TaskId,
                    VisionReviewStatus.Failed,
                    null,
                    cancellationToken);
            }
            catch
            {
                // Prefer the original failure when status update also fails.
            }

            throw;
        }
    }

    private static string Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        return text[..maxChars] + "\n…[truncated]";
    }
}
