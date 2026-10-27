namespace Hackathon.Application.InterviewTasks;

public interface IInteractiveAnswerStore
{
    Task SaveAsync(
        string userId,
        string sessionId,
        string taskId,
        Stream content,
        string contentType,
        string fileExtension,
        CancellationToken cancellationToken);
}
