using Hackathon.Application.Context;
using Hackathon.Application.InterviewProgress;
using Hackathon.Application.InterviewSessions;
using Hackathon.Application.InterviewTasks;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.VoiceAgent;

public sealed class EndVoiceConversationCommandHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewProgressStore progressStore,
    IPendingInterviewTaskStore pendingTasks)
    : IRequestHandler<EndVoiceConversationCommand, bool>
{
    public async Task<bool> Handle(
        EndVoiceConversationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ConversationId);

        var session = await sessionStore.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (await progressStore.HasAnyAsync(request.SessionId, cancellationToken)
            || await pendingTasks.HasOpenAsync(request.SessionId, cancellationToken))
        {
            return true;
        }

        await sessionStore.UpdateStatusAsync(
            userContext.UserId,
            request.SessionId,
            InterviewSessionStatus.Completed,
            cancellationToken);

        return true;
    }
}
