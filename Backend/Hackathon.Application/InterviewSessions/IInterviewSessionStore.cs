using Hackathon.Domain.InterviewSessions;

namespace Hackathon.Application.InterviewSessions;

public interface IInterviewSessionStore
{
    Task SaveAsync(InterviewSession session, CancellationToken cancellationToken);
}
