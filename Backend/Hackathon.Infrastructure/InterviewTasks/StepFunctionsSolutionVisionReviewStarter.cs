using System.Text.Json;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Hackathon.Application.InterviewTasks;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewTasks;

public sealed class StepFunctionsSolutionVisionReviewStarter(
    IAmazonStepFunctions stepFunctions,
    IOptions<SolutionVisionReviewOptions> options) : ISolutionVisionReviewStarter
{
    public async Task StartAsync(
        string userId,
        string sessionId,
        string taskId,
        string answerKey,
        CancellationToken cancellationToken)
    {
        // Unique per submit (retake reuses task ids); keep under 80 chars.
        var executionName = $"{sessionId}-{taskId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        if (executionName.Length > 80)
        {
            executionName = executionName[..80];
        }

        await stepFunctions.StartExecutionAsync(
            new StartExecutionRequest
            {
                StateMachineArn = options.Value.StateMachineArn,
                Name = executionName,
                Input = JsonSerializer.Serialize(
                    new Dictionary<string, string>
                    {
                        ["user_id"] = userId,
                        ["session_id"] = sessionId,
                        ["task_id"] = taskId,
                        ["answer_key"] = answerKey,
                    }),
            },
            cancellationToken);
    }
}
