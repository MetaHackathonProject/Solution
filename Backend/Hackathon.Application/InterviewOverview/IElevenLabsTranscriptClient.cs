namespace Hackathon.Application.InterviewOverview;

public interface IElevenLabsTranscriptClient
{
    Task<IReadOnlyList<TranscriptTurn>> GetTranscriptAsync(
        string conversationId,
        CancellationToken cancellationToken);
}

public sealed record TranscriptTurn(string Role, string Content);
