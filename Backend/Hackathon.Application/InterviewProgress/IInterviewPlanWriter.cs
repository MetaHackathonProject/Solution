namespace Hackathon.Application.InterviewProgress;

public interface IInterviewPlanWriter
{
    Task PutAsync(
        string userId,
        string sessionId,
        InterviewPlan plan,
        CancellationToken cancellationToken);
}
