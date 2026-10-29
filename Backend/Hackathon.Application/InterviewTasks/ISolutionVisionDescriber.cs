namespace Hackathon.Application.InterviewTasks;

public interface ISolutionVisionDescriber
{
    Task<string> DescribeAsync(
        byte[] content,
        string contentType,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken);
}
