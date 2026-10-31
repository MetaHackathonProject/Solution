namespace Hackathon.Application.InterviewSessions;

public interface IInterviewRecordingStore
{
    string CreateUploadUrl(string userId, string sessionId, string contentType, string objectKey);

    string CreateDownloadUrl(string objectKey);

    Task<IReadOnlyList<RecordingVideoObject>> ListVideosAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RecordingFrameObject>> ListFramesAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);

    Task DeleteBySessionAsync(string userId, string sessionId, CancellationToken cancellationToken);
}

public sealed record RecordingFrameObject(string Key, byte[] Content, string ContentType);

public sealed record RecordingVideoObject(string Key, string ContentType, long Size);
