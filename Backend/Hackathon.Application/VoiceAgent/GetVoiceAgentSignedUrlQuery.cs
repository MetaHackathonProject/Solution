using MediatR;

namespace Hackathon.Application.VoiceAgent;

public sealed record GetVoiceAgentSignedUrlQuery(string SessionId)
    : IRequest<VoiceAgentSignedUrlResult?>;

public sealed record VoiceAgentSignedUrlResult(
    string SignedUrl,
    string ConversationId,
    bool IsResume);
