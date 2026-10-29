namespace Hackathon.Application.InterviewTasks;

public sealed record InteractiveAnswerObject(
    byte[] Content,
    string ContentType,
    string Key);

public interface IInteractiveAnswerReader
{
    Task<InteractiveAnswerObject?> GetByKeyAsync(
        string answerKey,
        CancellationToken cancellationToken);
}
