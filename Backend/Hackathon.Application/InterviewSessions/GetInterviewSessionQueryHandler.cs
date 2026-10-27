using Hackathon.Application.Context;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed class GetInterviewSessionQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore)
    : IRequestHandler<GetInterviewSessionQuery, InterviewSession?>
{
    public Task<InterviewSession?> Handle(
        GetInterviewSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        return sessionStore.GetAsync(userContext.UserId, request.SessionId, cancellationToken);
    }
}
