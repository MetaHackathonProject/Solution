using Hackathon.Application.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewProgress;

public sealed class SeedInterviewProgressCommandHandler(
    IInterviewPlanReader planReader,
    IInterviewProgressStore progressStore,
    IPendingInterviewTaskStore pendingTasks) : IRequestHandler<SeedInterviewProgressCommand, SeedInterviewProgressResult>
{
    public async Task<SeedInterviewProgressResult> Handle(
        SeedInterviewProgressCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var plan = await planReader.GetAsync(request.UserId, request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Interview plan not found for user {request.UserId}, session {request.SessionId}.");

        if (plan.Sections is null || plan.Sections.Count == 0)
        {
            throw new InvalidOperationException(
                $"Interview plan for session {request.SessionId} has no sections.");
        }

        var items = new List<InterviewProgressItem>();
        foreach (var section in plan.Sections.OrderBy(section => section.ModuleOrder))
        {
            if (section.ModuleOrder < 0)
            {
                throw new InvalidOperationException(
                    $"Module order must be non-negative for module {section.Module}.");
            }

            foreach (var question in section.Questions.OrderBy(question => question.Order))
            {
                items.Add(await ToProgressItemAsync(
                    request.SessionId,
                    section,
                    question,
                    cancellationToken));
            }
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                $"Interview plan for session {request.SessionId} has no questions.");
        }

        await progressStore.ReplaceSessionAsync(request.SessionId, items, cancellationToken);

        return new SeedInterviewProgressResult(items.Count);
    }

    private async Task<InterviewProgressItem> ToProgressItemAsync(
        string sessionId,
        InterviewPlanSection section,
        InterviewPlanQuestion question,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question.Id))
        {
            throw new InvalidOperationException(
                $"Plan question in module {section.Module} is missing id.");
        }

        if (question.Order < 0)
        {
            throw new InvalidOperationException(
                $"Question order must be non-negative for question {question.Id}.");
        }

        string questionContent;
        string questionObjective;
        string? taskId = null;

        if (question.IsInteractive)
        {
            var task = await pendingTasks.GetAsync(sessionId, question.TaskId!, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Interactive task {question.TaskId} not found for session {sessionId}.");

            questionContent = task.QuestionContent;
            questionObjective = string.IsNullOrWhiteSpace(task.AssessmentGoal)
                ? question.AssessmentGoal
                : task.AssessmentGoal;
            taskId = task.TaskId;
        }
        else
        {
            questionContent = question.Question;
            questionObjective = question.AssessmentGoal;
        }

        return new InterviewProgressItem(
            sessionId,
            question.Id,
            section.Module,
            section.ModuleOrder,
            question.Order,
            BuildOrderKey(section.ModuleOrder, question.Order),
            questionContent,
            section.Objective,
            questionObjective,
            taskId);
    }

    internal static string BuildOrderKey(int moduleOrder, int questionOrder) =>
        $"{moduleOrder:D4}#{questionOrder:D4}";
}
