using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewOverview;

public sealed class GetInterviewOverviewQueryHandler(
    IInterviewSessionAccess access,
    IInterviewOverviewArtifactStore artifactStore)
    : IRequestHandler<GetInterviewOverviewQuery, InterviewResultOverview?>
{
    public async Task<InterviewResultOverview?> Handle(
        GetInterviewOverviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var resolved = await access.ResolveAsync(request.SessionId, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        if (!resolved.CanSeeOverview)
        {
            throw new UnauthorizedAccessException("Overview is only available to the employer.");
        }

        if (resolved.Session.OverviewStatus != OverviewStatus.Ready)
        {
            return null;
        }

        var overview = await artifactStore.GetOverviewAsync(
            resolved.Session.UserId,
            request.SessionId,
            cancellationToken);

        if (overview is null || resolved.CanSeeCameraOverview)
        {
            return overview;
        }

        return overview with { CameraPresence = null };
    }
}
