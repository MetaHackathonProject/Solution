namespace Hackathon.Application.InterviewOverview;

public interface IInterviewOverviewArtifactStore
{
    Task PutTranscriptAsync(
        string userId,
        string sessionId,
        string transcriptJson,
        CancellationToken cancellationToken);

    Task PutJudgmentAsync(
        string userId,
        string sessionId,
        string taskId,
        InteractiveJudgment judgment,
        CancellationToken cancellationToken);

    Task<InteractiveJudgment?> GetJudgmentAsync(
        string userId,
        string sessionId,
        string taskId,
        CancellationToken cancellationToken);

    Task PutOverviewAsync(
        string userId,
        string sessionId,
        InterviewResultOverview overview,
        CancellationToken cancellationToken);

    Task<InterviewResultOverview?> GetOverviewAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);

    Task<string?> GetTranscriptAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);
}
