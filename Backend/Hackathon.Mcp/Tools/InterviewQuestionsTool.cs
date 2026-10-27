using System.ComponentModel;
using Hackathon.Application.InterviewProgress;
using MediatR;
using ModelContextProtocol.Server;

namespace Hackathon.Mcp.Tools;

[McpServerToolType]
public sealed class InterviewQuestionsTool(IMediator mediator)
{
    [McpServerTool(Name = "get_next_question"), Description(
        "Get next question for interview session. " +
        "Returns two questions that agent has to ask and remaining questions for the interview session")]
    public Task<PopNextInterviewQuestionsResult> PopQuestionForInterview(
        string sessionId,
        CancellationToken cancellationToken)
        => mediator.Send(new PopNextInterviewQuestionsCommand(sessionId), cancellationToken);
}
