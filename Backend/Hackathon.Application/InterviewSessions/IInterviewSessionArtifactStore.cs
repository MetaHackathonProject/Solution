namespace Hackathon.Application.InterviewSessions;

public interface IInterviewSessionArtifactStore
{
    Task DeleteAsync(string userId, string sessionId, CancellationToken cancellationToken);
}
