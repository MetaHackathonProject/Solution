using Hackathon.Application.Context;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed class ListInterviewSessionsQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore)
    : IRequestHandler<ListInterviewSessionsQuery, IReadOnlyList<InterviewSession>>
{
    public Task<IReadOnlyList<InterviewSession>> Handle(
        ListInterviewSessionsQuery request,
        CancellationToken cancellationToken)
    {
        return sessionStore.ListByUserAsync(userContext.UserId, cancellationToken);
    }
}
