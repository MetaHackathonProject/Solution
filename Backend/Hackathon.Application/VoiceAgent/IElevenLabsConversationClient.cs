namespace Hackathon.Application.VoiceAgent;

public interface IElevenLabsConversationClient
{
    Task<ElevenLabsSignedConversation> GetSignedUrlAsync(
        VoiceAgentPurpose purpose,
        CancellationToken cancellationToken);
}

public sealed record ElevenLabsSignedConversation(string SignedUrl, string ConversationId);
