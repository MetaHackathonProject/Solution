using MediatR;

namespace Hackathon.Application.VoiceAgent;

public sealed record GetVoiceAgentSignedUrlQuery(
    string SessionId,
    VoiceAgentPurpose Purpose = VoiceAgentPurpose.Interview)
    : IRequest<VoiceAgentSignedUrlResult?>;

public sealed record VoiceAgentSignedUrlResult(
    string SignedUrl,
    string ConversationId,
    bool IsResume);
