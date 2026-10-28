using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.VoiceAgent;

public sealed class GetVoiceAgentSignedUrlQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IElevenLabsConversationClient elevenLabs)
    : IRequestHandler<GetVoiceAgentSignedUrlQuery, VoiceAgentSignedUrlResult?>
{
    public async Task<VoiceAgentSignedUrlResult?> Handle(
        GetVoiceAgentSignedUrlQuery request,
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

        if (session.Status is not (InterviewSessionStatus.Success or InterviewSessionStatus.InProgress))
        {
            throw new InvalidOperationException(
                $"Session {request.SessionId} is not ready for a live interview.");
        }

        var isResume = session.Status == InterviewSessionStatus.InProgress;

        var signed = await elevenLabs.GetSignedUrlAsync(request.Purpose, cancellationToken);

        await sessionStore.UpdateStatusAsync(
            userContext.UserId,
            request.SessionId,
            InterviewSessionStatus.InProgress,
            cancellationToken);

        return new VoiceAgentSignedUrlResult(signed.SignedUrl, signed.ConversationId, isResume);
    }
}
