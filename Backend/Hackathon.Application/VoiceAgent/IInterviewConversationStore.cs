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
}
