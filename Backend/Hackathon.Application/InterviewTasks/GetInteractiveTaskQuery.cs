using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed record GetInteractiveTaskQuery(string SessionId, string TaskId)
    : IRequest<ActiveInteractiveTaskResult?>;
