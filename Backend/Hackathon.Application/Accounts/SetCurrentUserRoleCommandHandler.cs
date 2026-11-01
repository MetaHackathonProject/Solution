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
        var email = FirstNonEmpty(request.Email, userContext.Email, existing?.Email);
        var profile = existing is null
            ? new UserProfile(userContext.UserId, role, now, now, email)
            : existing with { Email = email, UpdatedAt = string.Equals(existing.Email, email, StringComparison.Ordinal) ? existing.UpdatedAt : now };
        if (existing is null || !string.Equals(existing.Email, email, StringComparison.Ordinal))
        {
            await profiles.SaveAsync(profile, cancellationToken);
        }

        return new CurrentUserResult(userContext.UserId, email, profile.Role);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
