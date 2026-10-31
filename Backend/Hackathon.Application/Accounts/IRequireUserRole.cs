using Hackathon.Domain.Accounts;

namespace Hackathon.Application.Accounts;

public static class UserRoleGuard
{
    public static async Task<UserProfile> RequireAsync(
        IUserProfileStore profiles,
        string userId,
        string expectedRole,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userId, cancellationToken);
        if (profile is null)
        {
            throw new InvalidOperationException("Choose a role before continuing.");
        }

        if (!string.Equals(profile.Role, expectedRole, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                expectedRole == UserRoles.Employer
                    ? "Only employers can do this."
                    : "Only candidates can do this.");
        }

        return profile;
    }
}
