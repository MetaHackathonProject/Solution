using Hackathon.Application.Accounts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hackathon.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/me")]
public sealed class MeController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CurrentUserResponse>> Get(CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(ToResponse(user));
    }

    [HttpPut]
    public async Task<ActionResult<CurrentUserResponse>> SetRole(
        SetRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await mediator.Send(new SetCurrentUserRoleCommand(request.Role), cancellationToken);
            return Ok(ToResponse(user));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    private static CurrentUserResponse ToResponse(CurrentUserResult user) =>
        new(user.UserId, user.Email, user.Role);
}

public sealed class SetRoleRequest
{
    public required string Role { get; init; }
}

public sealed record CurrentUserResponse(string UserId, string Email, string? Role);
