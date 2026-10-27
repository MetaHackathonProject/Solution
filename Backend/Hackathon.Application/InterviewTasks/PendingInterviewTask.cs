namespace Hackathon.Application.InterviewTasks;

public sealed record PendingInterviewTask(
    string SessionId,
    string TaskId,
    string Module,
    string QuestionContentFormatted,
    string QuestionContent,
    string Status,
    string ExpectedInput,
    string? AssessmentGoal);
