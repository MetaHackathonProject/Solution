using Amazon.DynamoDBv2;
using Amazon.StepFunctions;
using Hackathon.Application.InterviewPreparation;
using Hackathon.Application.InterviewSessions;
using Hackathon.Infrastructure.InterviewPreparation;
using Hackathon.Infrastructure.InterviewSessions;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon.Infrastructure.Register;

public static class RegisterInDI
{
    public static void RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InterviewSessionsOptions>(configuration.GetSection(InterviewSessionsOptions.SectionName));
        services.Configure<InterviewPlannerOptions>(configuration.GetSection(InterviewPlannerOptions.SectionName));

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
        services.AddSingleton<IAmazonStepFunctions>(_ => new AmazonStepFunctionsClient());
        services.AddSingleton<IInterviewSessionStore, DynamoDbInterviewSessionStore>();
        services.AddSingleton<IInterviewPreparationStarter, StepFunctionsInterviewPreparationStarter>();
    }
}
