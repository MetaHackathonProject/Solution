using MediatR;

namespace Hackathon.Application.Accounts;

public sealed record SetCurrentUserRoleCommand(string Role, string? Email = null)
    : IRequest<CurrentUserResult>;
