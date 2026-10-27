using MediatR;

namespace Hackathon.Application.VoiceAgent;

public sealed record EndVoiceConversationCommand(string SessionId, string ConversationId)
    : IRequest<bool>;
