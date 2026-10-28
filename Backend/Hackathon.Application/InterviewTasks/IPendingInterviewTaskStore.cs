namespace Hackathon.Application.InterviewTasks;

public interface IPendingInterviewTaskStore
{
    Task<PendingInterviewTask?> GetAsync(
        string sessionId,
        string taskId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingInterviewTask>> ListBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task UpdateStatusAsync(
        string sessionId,
        string taskId,
        string status,
        CancellationToken cancellationToken);

    Task ResetAllToNotAskedAsync(string sessionId, CancellationToken cancellationToken);

    Task<bool> HasOpenAsync(string sessionId, CancellationToken cancellationToken);
}
