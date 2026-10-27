namespace Hackathon.Application.InterviewProgress;

public interface IInterviewProgressStore
{
    Task ReplaceSessionAsync(
        string sessionId,
        IReadOnlyList<InterviewProgressItem> items,
        CancellationToken cancellationToken);
}
