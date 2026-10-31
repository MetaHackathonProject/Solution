using MediatR;

namespace Hackathon.Application.Accounts;

public sealed record GetCurrentUserQuery : IRequest<CurrentUserResult>;

public sealed record CurrentUserResult(string UserId, string Email, string? Role);
