using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed record SubmitInteractiveAnswerCommand(
    string SessionId,
    string TaskId,
    Stream Content,
    string ContentType,
    string FileExtension) : IRequest<SubmitInteractiveAnswerResult?>;

public sealed record SubmitInteractiveAnswerResult(string VisionReviewStatus);
