using Hackathon.Application.Context;
using Hackathon.Application.InterviewTemplates;

namespace Hackathon.Application.InterviewSessions;

public sealed class InterviewSessionAccessResolver(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewAssignmentStore assignments) : IInterviewSessionAccess
{
    public async Task<InterviewSessionAccess?> ResolveAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var owned = await sessionStore.GetAsync(
            userContext.UserId,
            sessionId,
            cancellationToken);
        if (owned is not null)
        {
            return new InterviewSessionAccess(owned, IsOwner: true);
        }

        var assignment = await assignments.GetBySessionAsync(sessionId, cancellationToken);
        if (assignment is null ||
            !string.Equals(assignment.EmployerUserId, userContext.UserId, StringComparison.Ordinal))
        {
            return null;
        }

        var assigned = await sessionStore.GetAsync(
            assignment.CandidateUserId,
            sessionId,
            cancellationToken);
        return assigned is null ? null : new InterviewSessionAccess(assigned, IsOwner: false);
    }
}
