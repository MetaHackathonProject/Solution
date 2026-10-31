namespace Hackathon.Application.InterviewSessions;

public interface IOfferDetailsWriter
{
    Task PutAsync(
        string userId,
        string sessionId,
        string title,
        string companyName,
        string overviewForVoiceAgent,
        CancellationToken cancellationToken);
}
