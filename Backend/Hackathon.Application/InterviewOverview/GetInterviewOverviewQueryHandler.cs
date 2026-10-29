using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewOverview;

public sealed class GetInterviewOverviewQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewOverviewArtifactStore artifactStore)
    : IRequestHandler<GetInterviewOverviewQuery, InterviewResultOverview?>
{
    public async Task<InterviewResultOverview?> Handle(
        GetInterviewOverviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var session = await sessionStore.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.OverviewStatus != OverviewStatus.Ready)
        {
            return null;
        }

        return await artifactStore.GetOverviewAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);
    }
}
