namespace Hackathon.Application.InterviewQuestions;

public sealed record QuestionHelper(
    string QuestionId,
    string Module,
    string Question,
    string SuggestedAnswer,
    IReadOnlyList<string> Hints);
