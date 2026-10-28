using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed class SubmitInteractiveAnswerCommandHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IPendingInterviewTaskStore pendingTasks,
    IInteractiveAnswerStore answerStore,
    ISolutionVisionReviewStarter visionReviewStarter)
    : IRequestHandler<SubmitInteractiveAnswerCommand, SubmitInteractiveAnswerResult?>
{
    public async Task<SubmitInteractiveAnswerResult?> Handle(
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

        if (!VisionReviewNeeds.RequiresVisionReview(
                task.ExpectedInput,
                request.ContentType,
                request.FileExtension))
        {
            await pendingTasks.SetVisionReviewAsync(
                request.SessionId,
                request.TaskId,
                VisionReviewStatus.Skipped,
                null,
                cancellationToken);

            return new SubmitInteractiveAnswerResult(VisionReviewStatus.Skipped);
        }

        var answerKey = VisionReviewNeeds.BuildAnswerKey(
            userContext.UserId,
            request.SessionId,
            request.TaskId,
            request.FileExtension);

        await pendingTasks.SetVisionReviewAsync(
            request.SessionId,
            request.TaskId,
            VisionReviewStatus.Pending,
            null,
            cancellationToken);

        await visionReviewStarter.StartAsync(
            userContext.UserId,
            request.SessionId,
            request.TaskId,
            answerKey,
            cancellationToken);

        return new SubmitInteractiveAnswerResult(VisionReviewStatus.Pending);
    }
}
