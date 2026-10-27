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

        var ordered = await progressStore.ListOrderedAsync(request.SessionId, cancellationToken);
        if (ordered.Count == 0)
        {
            return new PopNextInterviewQuestionsResult([], 0);
        }

        var first = ordered[0];
        IReadOnlyList<InterviewProgressItem> selected;

        if (first.IsInteractive)
        {
            selected = [first];
        }
        else
        {
            selected = ordered.Take(QuestionsToPop).ToList();
        }

        var popped = await progressStore.PopNextAsync(
            request.SessionId,
            selected.Count,
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