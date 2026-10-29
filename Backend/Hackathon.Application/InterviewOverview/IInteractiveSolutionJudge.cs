namespace Hackathon.Application.InterviewOverview;

public interface IInteractiveSolutionJudge
{
    Task<InteractiveJudgment> JudgeVisualAsync(
        byte[] content,
        string contentType,
        string taskId,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken);

    Task<InteractiveJudgment> JudgeCodeAsync(
        byte[] zipContent,
        string taskId,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken);

    Task<InteractiveJudgment> JudgeTextAsync(
        string text,
        string taskId,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken);
}
