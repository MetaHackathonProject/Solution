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
    private static readonly HashSet<string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/webm",
        "video/mp4",
    };

    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
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

        var kind = (request.Kind ?? "segment").Trim().ToLowerInvariant();
        var contentType = NormalizeContentType(request.ContentType, kind);
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = Guid.NewGuid().ToString("N");
        var folder = kind == "frame" ? "frames" : "segments";
        var objectKey =
            $"{userContext.UserId}/{request.SessionId}/recording/{folder}/{stamp}-{id}.{ExtensionFor(contentType)}";
        var uploadUrl = recordingStore.CreateUploadUrl(
            userContext.UserId,
            request.SessionId,
            contentType,
            objectKey);

        return new RecordingUploadUrl(uploadUrl, objectKey, contentType);
    }

    private static string NormalizeContentType(string? contentType, string kind)
    {
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 128)
        {
            throw new InvalidOperationException("Unsupported recording content type.");
        }

        var trimmed = contentType.Trim();
        var mediaType = trimmed.Split(';', 2)[0].Trim();
        var allowed = kind == "frame" ? ImageTypes : VideoTypes;
        if (!allowed.Contains(mediaType))
        {
            throw new InvalidOperationException("Unsupported recording content type.");
        }

        return trimmed;
    }

    private static string ExtensionFor(string contentType)
    {
        var mediaType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return mediaType switch
        {
            "video/mp4" => "mp4",
            "image/png" => "png",
            "image/webp" => "webp",
            "image/jpeg" or "image/jpg" => "jpg",
            _ => "webm",
        };
    }
}
