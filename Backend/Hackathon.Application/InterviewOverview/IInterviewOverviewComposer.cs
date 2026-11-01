namespace Hackathon.Application.InterviewOverview;

public interface IInterviewOverviewComposer
{
    Task<InterviewResultOverview> ComposeAsync(
        string transcriptJson,
        IReadOnlyList<InteractiveJudgment> judgments,
        string feedbackLanguage,
        bool forEmployer,
        CancellationToken cancellationToken);
}
