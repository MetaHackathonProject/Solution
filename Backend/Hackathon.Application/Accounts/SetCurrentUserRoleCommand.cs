using MediatR;

namespace Hackathon.Application.Accounts;

public sealed record SetCurrentUserRoleCommand(string Role) : IRequest<CurrentUserResult>;
