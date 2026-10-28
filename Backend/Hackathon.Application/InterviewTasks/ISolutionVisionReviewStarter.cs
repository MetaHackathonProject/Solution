namespace Hackathon.Application.InterviewTasks;

public interface ISolutionVisionReviewStarter
{
    Task StartAsync(
        string userId,
        string sessionId,
        string taskId,
        string answerKey,
        CancellationToken cancellationToken);
}
