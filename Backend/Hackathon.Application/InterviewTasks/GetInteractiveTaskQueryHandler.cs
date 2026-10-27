using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed class GetInteractiveTaskQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IPendingInterviewTaskStore pendingTasks)
    : IRequestHandler<GetInteractiveTaskQuery, ActiveInteractiveTaskResult?>
{
    public async Task<ActiveInteractiveTaskResult?> Handle(
        GetInteractiveTaskQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskId);

        var session = await sessionStore.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);

        if (session is null)
        {
            return null;
        }

        var task = await pendingTasks.GetAsync(
            request.SessionId,
            request.TaskId,
            cancellationToken);

        if (task is null)
        {
            return null;
        }

        return new ActiveInteractiveTaskResult(
            task.TaskId,
            task.Module,
            task.QuestionContentFormatted,
            task.QuestionContent,
            task.Status,
            task.ExpectedInput);
    }
}
