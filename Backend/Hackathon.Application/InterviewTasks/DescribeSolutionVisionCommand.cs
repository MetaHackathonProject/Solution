using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed record DescribeSolutionVisionCommand(
    string UserId,
    string SessionId,
    string TaskId,
    string AnswerKey) : IRequest<DescribeSolutionVisionResult>;

public sealed record DescribeSolutionVisionResult(
    string VisionReviewStatus,
    int DescriptionChars);
