namespace Hackathon.Application.InterviewProgress;

public interface IInterviewProgressStore
{
    Task ReplaceSessionAsync(
        string sessionId,
        IReadOnlyList<InterviewProgressItem> items,
        CancellationToken cancellationToken);

    Task<PopInterviewProgressResult> PopNextAsync(
        string sessionId,
        int count,
        CancellationToken cancellationToken);
}

public sealed record PopInterviewProgressResult(
    IReadOnlyList<InterviewProgressItem> Questions,
    int QuestionsRemaining);
