namespace Hackathon.Application.InterviewQuestions;

public interface IInterviewQuestionHelperStore
{
    Task SetActiveQuestionIdsAsync(
        string sessionId,
        IReadOnlyList<string> questionIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetActiveQuestionIdsAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task ClearActiveQuestionIdsAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<QuestionHelper>> GetByIdsAsync(
        string sessionId,
        IReadOnlyList<string> questionIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<QuestionHelper>> ListWithHelpersAsync(
        string sessionId,
        CancellationToken cancellationToken);
}
