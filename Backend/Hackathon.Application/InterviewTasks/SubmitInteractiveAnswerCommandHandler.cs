using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed class SubmitInteractiveAnswerCommandHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IPendingInterviewTaskStore pendingTasks,
    IInteractiveAnswerStore answerStore)
    : IRequestHandler<SubmitInteractiveAnswerCommand, bool>
{
    public async Task<bool> Handle(
        SubmitInteractiveAnswerCommand request,
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
            return false;
        }

        var task = await pendingTasks.GetAsync(
            request.SessionId,
            request.TaskId,
            cancellationToken);

        if (task is null)
        {
            return false;
        }

        if (task.Status == InteractiveTaskStatus.Answered)
        {
            throw new InvalidOperationException("This interactive task was already answered.");
        }

        await answerStore.SaveAsync(
            userContext.UserId,
            request.SessionId,
            request.TaskId,
            request.Content,
            request.ContentType,
            request.FileExtension,
            cancellationToken);

        await pendingTasks.UpdateStatusAsync(
            request.SessionId,
            request.TaskId,
            InteractiveTaskStatus.Answered,
            cancellationToken);

        return true;
    }
}
