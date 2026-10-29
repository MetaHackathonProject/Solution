using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Hackathon.Application.InterviewTasks;
using Hackathon.Application.Register;
using Hackathon.Infrastructure.Register;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Hackathon.DescribeSolutionVision;

public sealed class Function
{
    private readonly IServiceProvider _serviceProvider;

    public Function() : this(BuildServiceProvider())
    {
    }

    internal Function(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<DescribeSolutionVisionResponse> FunctionHandler(
        DescribeSolutionVisionRequest request,
        ILambdaContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();

        logger.LogInformation(
            "Describing solution vision. SessionId: {SessionId}, TaskId: {TaskId}, AnswerKey: {AnswerKey}",
            request.SessionId,
            request.TaskId,
            request.AnswerKey);

        var result = await mediator.Send(
            new DescribeSolutionVisionCommand(
                request.UserId,
                request.SessionId,
                request.TaskId,
                request.AnswerKey));

        logger.LogInformation(
            "Vision review {Status} ({Chars} chars) for session {SessionId} task {TaskId}",
            result.VisionReviewStatus,
            result.DescriptionChars,
            request.SessionId,
            request.TaskId);

        return new DescribeSolutionVisionResponse(
            result.VisionReviewStatus,
            result.DescriptionChars);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables(prefix: "APP_")
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });
        services.RegisterApplication();
        services.RegisterInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}

public sealed record DescribeSolutionVisionResponse(
    [property: JsonPropertyName("vision_review_status")] string VisionReviewStatus,
    [property: JsonPropertyName("description_chars")] int DescriptionChars);
