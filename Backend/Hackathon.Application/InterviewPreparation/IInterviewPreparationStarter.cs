namespace Hackathon.Application.InterviewPreparation;

public interface IInterviewPreparationStarter
{
    Task StartAsync(
        string userId,
        string sessionId,
        string offerUrl,
        string recruiterLanguage,
        CancellationToken cancellationToken);
}
