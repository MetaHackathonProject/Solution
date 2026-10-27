using Hackathon.Domain.InterviewSessions;

namespace Hackathon.Application.InterviewSessions;

public interface IInterviewSessionStore
{
    Task SaveAsync(InterviewSession session, CancellationToken cancellationToken);

    Task<InterviewSession?> GetAsync(string userId, string sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InterviewSession>> ListByUserAsync(string userId, CancellationToken cancellationToken);
}
