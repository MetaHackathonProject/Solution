using Hackathon.Application.Context;
using Hackathon.Domain.Accounts;
using MediatR;

namespace Hackathon.Application.Accounts;

public sealed class SetCurrentUserRoleCommandHandler(
    IUserContext userContext,
    IUserProfileStore profiles) : IRequestHandler<SetCurrentUserRoleCommand, CurrentUserResult>
{
    public async Task<CurrentUserResult> Handle(
        SetCurrentUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        var role = request.Role.Trim();
        if (!UserRoles.IsDefined(role))
        {
            throw new ArgumentException("Role must be Candidate or Employer.", nameof(request));
        }

        var existing = await profiles.GetAsync(userContext.UserId, cancellationToken);
        if (existing is not null &&
            !string.Equals(existing.Role, role, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Role is already set and cannot be changed.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var profile = existing ?? new UserProfile(userContext.UserId, role, now, now);
        if (existing is null)
        {
            await profiles.SaveAsync(profile, cancellationToken);
        }

        return new CurrentUserResult(userContext.UserId, userContext.Email, profile.Role);
    }
}
