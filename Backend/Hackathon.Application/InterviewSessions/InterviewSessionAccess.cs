using Hackathon.Domain.InterviewSessions;

namespace Hackathon.Application.InterviewSessions;

public sealed record InterviewSessionAccess(InterviewSession Session, bool IsOwner)
{
    public bool CanSeeCameraOverview => IsOwner ? !Session.HideCameraOverview : true;

    public bool CanPlay => IsOwner;

    public bool CanRetake => IsOwner && Session.RetakeAllowed;
}

public interface IInterviewSessionAccess
{
    Task<InterviewSessionAccess?> ResolveAsync(string sessionId, CancellationToken cancellationToken);
}
