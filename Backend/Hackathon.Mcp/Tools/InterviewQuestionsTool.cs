using System.ComponentModel;
using Hackathon.Application.InterviewProgress;
using MediatR;
using ModelContextProtocol.Server;

namespace Hackathon.Mcp.Tools;

[McpServerToolType]
public sealed class InterviewQuestionsTool(IMediator mediator)
{
    [McpServerTool(Name = "get_next_question"), Description(
        "Get the next question for the interview session. " +
        "Returns one spoken question (or one interactive item) and how many questions remain. " +
        "Call again after the candidate has answered before moving on.")]
    public Task<PopNextInterviewQuestionsResult> PopQuestionForInterview(
        string sessionId,
        CancellationToken cancellationToken)
        => mediator.Send(new PopNextInterviewQuestionsCommand(sessionId), cancellationToken);
}
