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

    Task<IReadOnlyList<InterviewProgressItem>> ListOrderedAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task PutAsync(InterviewProgressItem item, CancellationToken cancellationToken);

    Task<bool> HasAnyAsync(string sessionId, CancellationToken cancellationToken);
}

public sealed record PopInterviewProgressResult(
    IReadOnlyList<InterviewProgressItem> Questions,
    int QuestionsRemaining);
