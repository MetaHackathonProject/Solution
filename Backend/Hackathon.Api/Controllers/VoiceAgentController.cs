using Hackathon.Application.VoiceAgent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hackathon.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/voice-agent")]
public sealed class VoiceAgentController(IMediator mediator) : ControllerBase
{
    [HttpGet("signed-url")]
    public async Task<ActionResult<VoiceAgentSignedUrlResponse>> GetSignedUrl(
        [FromQuery] string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new GetVoiceAgentSignedUrlQuery(sessionId),
                cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(new VoiceAgentSignedUrlResponse(result.SignedUrl, result.ConversationId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("end-conversation")]
    public async Task<IActionResult> EndConversation(
        EndVoiceConversationRequest request,
        CancellationToken cancellationToken)
    {
        var found = await mediator.Send(
            new EndVoiceConversationCommand(request.SessionId, request.ConversationId),
            cancellationToken);

        return found ? NoContent() : NotFound();
    }
}

public sealed record VoiceAgentSignedUrlResponse(string SignedUrl, string ConversationId);

public sealed class EndVoiceConversationRequest
{
    public required string SessionId { get; init; }

    public required string ConversationId { get; init; }
}
