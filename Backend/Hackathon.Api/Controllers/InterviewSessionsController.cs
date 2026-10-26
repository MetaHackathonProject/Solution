using Hackathon.Application.InterviewPreparation;
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
    [HttpPost]
    public async Task<StartInterviewPreparationResult> Start(
        StartInterviewPreparationRequest request,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new StartInterviewPreparationCommand(
                request.Name,
                request.OfferUrl,
                new InterviewSessionSettings(request.Settings.RecruiterLanguage)),
            cancellationToken);
    }
}

public sealed class StartInterviewPreparationRequest
{
    public string? OfferUrl { get; init; }

    public required string Name { get; init; }

    public required InterviewSessionSettingsRequest Settings { get; init; }
}

public sealed class InterviewSessionSettingsRequest
{
    public required string RecruiterLanguage { get; init; }
}
