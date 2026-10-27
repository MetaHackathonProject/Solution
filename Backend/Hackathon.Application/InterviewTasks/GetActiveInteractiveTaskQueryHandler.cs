using Hackathon.Application.Context;
using Hackathon.Application.InterviewProgress;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed class GetActiveInteractiveTaskQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewProgressStore progressStore,
    IPendingInterviewTaskStore pendingTasks)
    : IRequestHandler<GetActiveInteractiveTaskQuery, ActiveInteractiveTaskResult?>
{
    public async Task<ActiveInteractiveTaskResult?> Handle(
        GetActiveInteractiveTaskQuery request,
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

        var tasks = await pendingTasks.ListBySessionAsync(request.SessionId, cancellationToken);

        var inProgress = tasks.FirstOrDefault(task =>
            task.Status == InteractiveTaskStatus.InProgress);
        if (inProgress is not null)
        {
            return ToResult(inProgress);
        }

        var ordered = await progressStore.ListOrderedAsync(request.SessionId, cancellationToken);
        var next = ordered.FirstOrDefault();
        if (next is null || !next.IsInteractive)
        {
            return null;
        }

        var pending = tasks.FirstOrDefault(task => task.TaskId == next.TaskId);
        if (pending is null)
        {
            return null;
        }

        if (pending.Status is not (InteractiveTaskStatus.NotAsked or InteractiveTaskStatus.InProgress))
        {
            return null;
        }

        return ToResult(pending);
    }

    private static ActiveInteractiveTaskResult ToResult(PendingInterviewTask task) =>
        new(
            task.TaskId,
            task.Module,
            task.QuestionContentFormatted,
            task.QuestionContent,
            task.Status,
            task.ExpectedInput);
}
