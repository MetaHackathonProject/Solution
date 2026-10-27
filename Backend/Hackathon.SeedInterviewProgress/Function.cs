using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Hackathon.Application.InterviewProgress;
using Hackathon.Application.Register;
using Hackathon.Infrastructure.Register;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Hackathon.SeedInterviewProgress;

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

    public async Task<SeedInterviewProgressResponse> FunctionHandler(
        SeedInterviewProgressRequest request,
        ILambdaContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();

        logger.LogInformation(
            "Seeding interview progress. UserId: {UserId}, SessionId: {SessionId}",
            request.UserId,
            request.SessionId);

        var result = await mediator.Send(
            new SeedInterviewProgressCommand(request.UserId, request.SessionId));

        logger.LogInformation(
            "Seeded {Count} progress items for session {SessionId}",
            result.QuestionsSeeded,
            request.SessionId);

        return new SeedInterviewProgressResponse(result.QuestionsSeeded);
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

public sealed record SeedInterviewProgressResponse(
    [property: JsonPropertyName("questions_seeded")] int QuestionsSeeded);
