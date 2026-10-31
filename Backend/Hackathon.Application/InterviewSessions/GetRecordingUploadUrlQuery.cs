using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed record GetRecordingUploadUrlQuery(string SessionId, string? ContentType)
    : IRequest<RecordingUploadUrl?>;

public sealed record RecordingUploadUrl(string UploadUrl, string ObjectKey, string ContentType);
