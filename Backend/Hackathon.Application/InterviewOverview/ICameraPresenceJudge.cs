namespace Hackathon.Application.InterviewOverview;

public interface ICameraPresenceJudge
{
    Task<CameraPresenceReview?> JudgeAsync(
        IReadOnlyList<CameraVideoLink> videos,
        IReadOnlyList<CameraStill> stills,
        CancellationToken cancellationToken);
}

public sealed record CameraVideoLink(string Url, string ContentType, long Size);

public sealed record CameraStill(byte[] Content, string ContentType);
