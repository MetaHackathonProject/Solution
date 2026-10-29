using Hackathon.Application.Context;
using Hackathon.Application.InterviewProgress;
using Hackathon.Application.InterviewQuestions;
using Hackathon.Application.InterviewTasks;
using Hackathon.Application.VoiceAgent;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed class RetakeInterviewSessionCommandHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IPendingInterviewTaskStore pendingTasks,
    IInterviewConversationStore conversationStore,
    IInterviewQuestionHelperStore questionHelpers,
    IMediator mediator)
    : IRequestHandler<RetakeInterviewSessionCommand, RetakeInterviewSessionResult?>
{
    public async Task<RetakeInterviewSessionResult?> Handle(
        RetakeInterviewSessionCommand request,
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

        if (session.Status is not (
            InterviewSessionStatus.Completed
            or InterviewSessionStatus.Success
            or InterviewSessionStatus.InProgress))
        {
            throw new InvalidOperationException(
                $"Session {request.SessionId} cannot be retaken while status is {session.Status}.");
        }

        await pendingTasks.ResetAllToNotAskedAsync(request.SessionId, cancellationToken);
        await conversationStore.DeleteBySessionAsync(request.SessionId, cancellationToken);
        await questionHelpers.ClearActiveQuestionIdsAsync(request.SessionId, cancellationToken);

        var seeded = await mediator.Send(
            new SeedInterviewProgressCommand(userContext.UserId, request.SessionId),
            cancellationToken);

        await sessionStore.UpdateStatusAsync(
            userContext.UserId,
            request.SessionId,
            InterviewSessionStatus.Success,
            cancellationToken);

        await sessionStore.UpdateOverviewStatusAsync(
            userContext.UserId,
            request.SessionId,
            OverviewStatus.NotAvailable,
            cancellationToken);

        return new RetakeInterviewSessionResult(request.SessionId, seeded.QuestionsSeeded);
    }
}
