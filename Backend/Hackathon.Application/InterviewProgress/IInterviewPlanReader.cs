namespace Hackathon.Application.InterviewProgress;

public interface IInterviewPlanReader
{
    Task<InterviewPlan?> GetAsync(string userId, string sessionId, CancellationToken cancellationToken);
}
