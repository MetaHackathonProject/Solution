namespace Hackathon.Application.InterviewSessions;

public interface IInterviewRecordingStore
{
    string CreateUploadUrl(string userId, string sessionId, string contentType, string objectKey);

    Task DeleteBySessionAsync(string userId, string sessionId, CancellationToken cancellationToken);
}
