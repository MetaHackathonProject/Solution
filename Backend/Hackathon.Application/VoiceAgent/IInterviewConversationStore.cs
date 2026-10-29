namespace Hackathon.Application.VoiceAgent;

public sealed record InterviewConversation(
    string SessionId,
    long CreatedAt,
    string ConversationId,
    string Purpose);

public interface IInterviewConversationStore
{
    Task SaveAsync(InterviewConversation conversation, CancellationToken cancellationToken);

    Task<IReadOnlyList<InterviewConversation>> ListBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken);

    /// <summary>Removes all stored ConvAI conversation ids for a session (used on retake).</summary>
    Task DeleteBySessionAsync(string sessionId, CancellationToken cancellationToken);
}
