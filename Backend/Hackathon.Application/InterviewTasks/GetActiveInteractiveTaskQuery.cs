using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed record GetActiveInteractiveTaskQuery(string SessionId)
    : IRequest<ActiveInteractiveTaskResult?>;

public sealed record ActiveInteractiveTaskResult(
    string TaskId,
    string Module,
    string QuestionContentFormatted,
    string QuestionContent,
    string Status,
    string ExpectedInput);
