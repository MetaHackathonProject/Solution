using System.Text.Json;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Hackathon.Application.InterviewOverview;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class StepFunctionsInterviewOverviewStarter(
    IAmazonStepFunctions stepFunctions,
    IOptions<InterviewOverviewOptions> options) : IInterviewOverviewStarter
{
    public async Task StartAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.StateMachineArn))
        {
            throw new InvalidOperationException("InterviewOverview StateMachineArn is not configured.");
        }

        var executionName = $"{sessionId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
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
                    }),
            },
            cancellationToken);
    }
}
