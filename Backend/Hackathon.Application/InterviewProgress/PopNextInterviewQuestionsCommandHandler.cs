using Hackathon.Application.InterviewTasks;
using Hackathon.Domain.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewProgress;

public sealed class PopNextInterviewQuestionsCommandHandler(
    IInterviewProgressStore progressStore,
    IPendingInterviewTaskStore pendingTasks)
    : IRequestHandler<PopNextInterviewQuestionsCommand, PopNextInterviewQuestionsResult>
{
    private const int QuestionsToPop = 2;

    public async Task<PopNextInterviewQuestionsResult> Handle(
        PopNextInterviewQuestionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var remaining = await progressStore.ListOrderedAsync(request.SessionId, cancellationToken);

        var inProgress = (await pendingTasks.ListBySessionAsync(request.SessionId, cancellationToken))
            .FirstOrDefault(task => task.Status == InteractiveTaskStatus.InProgress);

        if (inProgress is not null)
        {
            return new PopNextInterviewQuestionsResult(
                [
                    new InterviewQuestionResultItem(
                        inProgress.QuestionContent,
                        inProgress.AssessmentGoal ?? string.Empty,
                        string.Empty,
                        inProgress.Module,
                        "interactive",
                        inProgress.TaskId),
                ],
                remaining.Count);
        }

        if (remaining.Count == 0)
        {
            return new PopNextInterviewQuestionsResult([], 0);
        }

        var first = remaining[0];
        var countToPop = first.IsInteractive
            ? 1
            : Math.Min(QuestionsToPop, remaining.Count);

        // Never pop past an interactive item in the same batch: if the second item
        // would be interactive, only return the spoken first and leave interactive queued.
        if (!first.IsInteractive && countToPop > 1 && remaining[1].IsInteractive)
        {
            countToPop = 1;
        }

        var popped = await progressStore.PopNextAsync(
            request.SessionId,
            countToPop,
            cancellationToken);

        foreach (var item in popped.Questions.Where(question => question.IsInteractive))
        {
            await pendingTasks.UpdateStatusAsync(
                request.SessionId,
                item.TaskId!,
                InteractiveTaskStatus.InProgress,
                cancellationToken);
        }

        return new PopNextInterviewQuestionsResult(
            popped.Questions
                .Select(item => new InterviewQuestionResultItem(
                    item.QuestionContent,
                    item.QuestionObjective,
                    item.ModuleObjective,
                    item.Module,
                    item.IsInteractive ? "interactive" : null,
                    item.TaskId))
                .ToList(),
            popped.QuestionsRemaining);
    }
}
