namespace Hackathon.Application.InterviewOverview;

public interface IInterviewOverviewStarter
{
    Task StartAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);
}
