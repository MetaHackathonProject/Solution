using Hackathon.Application.Context;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed class GetRecordingUploadUrlQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewRecordingStore recordingStore)
    : IRequestHandler<GetRecordingUploadUrlQuery, RecordingUploadUrl?>
{
    private static readonly HashSet<string> AllowedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/webm",
        "video/mp4",
    };

    public async Task<RecordingUploadUrl?> Handle(
        GetRecordingUploadUrlQuery request,
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

        if (session.Status is not (
            InterviewSessionStatus.Success
            or InterviewSessionStatus.InProgress
            or InterviewSessionStatus.Completed))
        {
            throw new InvalidOperationException("This session is not ready for a camera recording upload.");
        }

        var contentType = NormalizeContentType(request.ContentType);
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var segmentId = Guid.NewGuid().ToString("N");
        var objectKey =
            $"{userContext.UserId}/{request.SessionId}/recording/segments/{stamp}-{segmentId}.{ExtensionFor(contentType)}";
        var uploadUrl = recordingStore.CreateUploadUrl(
            userContext.UserId,
            request.SessionId,
            contentType,
            objectKey);

        return new RecordingUploadUrl(uploadUrl, objectKey, contentType);
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 128)
        {
            throw new InvalidOperationException("Unsupported recording content type.");
        }

        var trimmed = contentType.Trim();
        var mediaType = trimmed.Split(';', 2)[0].Trim();
        if (!AllowedMediaTypes.Contains(mediaType))
        {
            throw new InvalidOperationException("Unsupported recording content type.");
        }

        return trimmed;
    }

    private static string ExtensionFor(string contentType)
    {
        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase) ? "mp4" : "webm";
    }
}
