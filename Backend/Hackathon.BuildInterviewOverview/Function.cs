using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Hackathon.Application.InterviewOverview;
using Hackathon.Application.Register;
using Hackathon.Infrastructure.Register;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Hackathon.BuildInterviewOverview;

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

    public async Task<BuildInterviewOverviewResponse> FunctionHandler(
        BuildInterviewOverviewRequest request,
        ILambdaContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();

        logger.LogInformation(
            "Building interview overview. UserId: {UserId}, SessionId: {SessionId}",
            request.UserId,
            request.SessionId);

        var result = await mediator.Send(
            new BuildInterviewOverviewCommand(request.UserId, request.SessionId));

        logger.LogInformation(
            "Overview {Status} score={Score} for session {SessionId}",
            result.OverviewStatus,
            result.Score,
            request.SessionId);

        return new BuildInterviewOverviewResponse(result.OverviewStatus, result.Score);
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

public sealed record BuildInterviewOverviewResponse(
    [property: JsonPropertyName("overview_status")] string OverviewStatus,
    [property: JsonPropertyName("score")] int Score);
