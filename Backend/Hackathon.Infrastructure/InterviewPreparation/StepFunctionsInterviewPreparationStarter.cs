using System.Text.Json;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Hackathon.Application.InterviewPreparation;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewPreparation;

public sealed class StepFunctionsInterviewPreparationStarter(
    IAmazonStepFunctions stepFunctions,
    IOptions<InterviewPlannerOptions> options) : IInterviewPreparationStarter
{
    public async Task StartAsync(
        string userId,
        string sessionId,
        string offerUrl,
        string recruiterLanguage,
        CancellationToken cancellationToken)
    {
        await stepFunctions.StartExecutionAsync(
            new StartExecutionRequest
            {
                StateMachineArn = options.Value.StateMachineArn,
                Name = sessionId,
                Input = JsonSerializer.Serialize(
                    new Dictionary<string, string>
                    {
                        ["user_id"] = userId,
                        ["session_id"] = sessionId,
                        ["offer_url"] = offerUrl,
                        ["recruiter_language"] = recruiterLanguage,
                    }),
            },
            cancellationToken);
    }
}
