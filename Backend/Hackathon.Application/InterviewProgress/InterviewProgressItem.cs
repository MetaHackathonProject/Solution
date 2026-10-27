namespace Hackathon.Application.InterviewProgress;

public sealed record InterviewProgressItem(
    string SessionId,
    string QuestionId,
    string Module,
    int ModuleOrder,
    int QuestionOrder,
    string OrderKey,
    string QuestionContent,
    string ModuleObjective,
    string QuestionObjective,
    string? TaskId = null)
{
    public bool IsInteractive => !string.IsNullOrWhiteSpace(TaskId);
}
