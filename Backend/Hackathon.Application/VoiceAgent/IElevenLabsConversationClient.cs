namespace Hackathon.Application.VoiceAgent;

public interface IElevenLabsConversationClient
{
    Task<ElevenLabsSignedConversation> GetSignedUrlAsync(CancellationToken cancellationToken);
}

public sealed record ElevenLabsSignedConversation(string SignedUrl, string ConversationId);
