using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed class GetInterviewSessionQueryHandler(IInterviewSessionAccess access)
    : IRequestHandler<GetInterviewSessionQuery, InterviewSessionAccess?>
{
    public Task<InterviewSessionAccess?> Handle(
        GetInterviewSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        return access.ResolveAsync(request.SessionId, cancellationToken);
    }
}
