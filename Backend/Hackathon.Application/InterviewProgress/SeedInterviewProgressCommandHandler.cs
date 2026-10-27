using MediatR;

namespace Hackathon.Application.InterviewProgress;

public sealed class SeedInterviewProgressCommandHandler(
    IInterviewPlanReader planReader,
    IInterviewProgressStore progressStore) : IRequestHandler<SeedInterviewProgressCommand, SeedInterviewProgressResult>
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

        var items = plan.Sections
            .OrderBy(section => section.ModuleOrder)
            .SelectMany(section =>
            {
                if (section.ModuleOrder < 0)
                {
                    throw new InvalidOperationException(
                        $"Module order must be non-negative for module {section.Module}.");
                }

                return section.Questions
                    .OrderBy(question => question.Order)
                    .Select(question => ToProgressItem(request.SessionId, section, question));
            })
            .ToList();

        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                $"Interview plan for session {request.SessionId} has no questions.");
        }

        await progressStore.ReplaceSessionAsync(request.SessionId, items, cancellationToken);

        return new SeedInterviewProgressResult(items.Count);
    }

    private static InterviewProgressItem ToProgressItem(
        string sessionId,
        InterviewPlanSection section,
        InterviewPlanQuestion question)
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

        return new InterviewProgressItem(
            sessionId,
            question.Id,
            section.Module,
            section.ModuleOrder,
            question.Order,
            BuildOrderKey(section.ModuleOrder, question.Order),
            question.Question,
            section.Objective,
            question.AssessmentGoal);
    }

    internal static string BuildOrderKey(int moduleOrder, int questionOrder) =>
        $"{moduleOrder:D4}#{questionOrder:D4}";
}
