using Hackathon.Application.Context;
using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed class DeleteInterviewSessionCommandHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewSessionArtifactStore artifactStore)
    : IRequestHandler<DeleteInterviewSessionCommand, bool>
{
    public async Task<bool> Handle(
        DeleteInterviewSessionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var session = await sessionStore.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);

        if (session is null)
        {
            return false;
        }

        await artifactStore.DeleteAsync(userContext.UserId, request.SessionId, cancellationToken);
        await sessionStore.DeleteAsync(userContext.UserId, request.SessionId, cancellationToken);
        return true;
    }
}
