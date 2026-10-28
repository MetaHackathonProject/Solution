using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed record GetVisionReviewQuery(string SessionId, string TaskId)
    : IRequest<VisionReviewResult?>;

public sealed record VisionReviewResult(
    string TaskId,
    string Status,
    string? Description);
