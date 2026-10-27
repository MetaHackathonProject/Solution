using Hackathon.Application.InterviewPreparation;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hackathon.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/interview-sessions")]
public sealed class InterviewSessionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InterviewSessionResponse>>> List(
        CancellationToken cancellationToken)
    {
        var sessions = await mediator.Send(new ListInterviewSessionsQuery(), cancellationToken);
        return Ok(sessions.Select(ToResponse).ToList());
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<InterviewSessionResponse>> Get(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var session = await mediator.Send(new GetInterviewSessionQuery(sessionId), cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(session));
    }

    [HttpPost]
    public async Task<StartInterviewPreparationResult> Start(
        StartInterviewPreparationRequest request,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new StartInterviewPreparationCommand(
                request.Name,
                request.OfferUrl,
                new InterviewSessionSettings(
                    request.Settings.RecruiterLanguage,
                    request.Settings.Difficulty)),
            cancellationToken);
    }

    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> Delete(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var deleted = await mediator.Send(
            new DeleteInterviewSessionCommand(sessionId),
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    private static InterviewSessionResponse ToResponse(InterviewSession session) =>
        new(
            session.SessionId,
            session.Name,
            session.OfferUrl,
            session.Status,
            session.CreatedAt,
            session.UpdatedAt,
            new InterviewSessionSettingsResponse(
                session.Settings.RecruiterLanguage,
                session.Settings.Difficulty));
}

public sealed class StartInterviewPreparationRequest
{
    public required string OfferUrl { get; init; }

    public required string Name { get; init; }

    public required InterviewSessionSettingsRequest Settings { get; init; }
}

public sealed class InterviewSessionSettingsRequest
{
    public required string RecruiterLanguage { get; init; }

    public required string Difficulty { get; init; }
}

public sealed record InterviewSessionResponse(
    string SessionId,
    string Name,
    string? OfferUrl,
    string Status,
    long CreatedAt,
    long UpdatedAt,
    InterviewSessionSettingsResponse Settings);

public sealed record InterviewSessionSettingsResponse(
    string RecruiterLanguage,
    string Difficulty);
