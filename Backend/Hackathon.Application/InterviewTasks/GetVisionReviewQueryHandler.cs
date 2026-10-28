using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewTasks;
using MediatR;

namespace Hackathon.Application.InterviewTasks;

public sealed class GetVisionReviewQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IPendingInterviewTaskStore pendingTasks)
    : IRequestHandler<GetVisionReviewQuery, VisionReviewResult?>
{
    public async Task<VisionReviewResult?> Handle(
        GetVisionReviewQuery request,
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

        var status = string.IsNullOrWhiteSpace(task.VisionReviewStatus)
            ? VisionReviewStatus.Skipped
            : task.VisionReviewStatus;

        return new VisionReviewResult(
            task.TaskId,
            status,
            string.IsNullOrWhiteSpace(task.VisionDescription) ? null : task.VisionDescription);
    }
}
