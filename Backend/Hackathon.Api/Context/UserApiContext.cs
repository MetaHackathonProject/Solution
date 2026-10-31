using System.Security.Claims;
using Hackathon.Application.Context;

namespace Hackathon.Api.Context;

public sealed class UserApiContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirstValue("username")
        ?? throw new ArgumentException("Invalid claims in authorization token");

    public string Email =>
        User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email")
        ?? string.Empty;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
