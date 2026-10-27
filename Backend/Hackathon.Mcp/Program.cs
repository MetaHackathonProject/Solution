using Hackathon.Application.Register;
using Hackathon.Infrastructure.Register;
using Hackathon.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var env = builder.Environment;

builder.Configuration.AddEnvironmentVariables(prefix: "APP_");

builder.Services.RegisterApplication();
builder.Services.RegisterInfrastructure(builder.Configuration);

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithTools<InterviewQuestionsTool>();

var app = builder.Build();

if (!env.IsDevelopment())
{
    app.UsePathBase("/mcp/interview-questions");
}

app.MapMcp(pattern: "agent");

app.Run();
