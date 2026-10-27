namespace Hackathon.Application.InterviewSessions;

public interface IOfferDetailsReader
{
    Task<OfferDetails?> GetAsync(string userId, string sessionId, CancellationToken cancellationToken);
}

public sealed record OfferDetails(
    string? Title,
    string? CompanyName,
    string? OverviewForVoiceAgent);
