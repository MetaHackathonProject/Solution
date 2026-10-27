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

// API Gateway forwards /mcp/interview-questions[/...]. Strip that prefix so MapMcp
// endpoints resolve. Keep both "" and "agent" so either public URL works.
if (!env.IsDevelopment())
{
    app.UsePathBase("/mcp/interview-questions");
}

app.MapMcp();
app.MapMcp(pattern: "agent");

app.Run();
