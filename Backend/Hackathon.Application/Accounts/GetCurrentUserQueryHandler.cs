using Hackathon.Application.Context;
using MediatR;

namespace Hackathon.Application.Accounts;

public sealed class GetCurrentUserQueryHandler(
    IUserContext userContext,
    IUserProfileStore profiles) : IRequestHandler<GetCurrentUserQuery, CurrentUserResult>
{
    public async Task<CurrentUserResult> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userContext.UserId, cancellationToken);
        var email = FirstNonEmpty(profile?.Email, userContext.Email);
        if (profile is not null &&
            !string.IsNullOrWhiteSpace(userContext.Email) &&
            !string.Equals(profile.Email, userContext.Email.Trim(), StringComparison.Ordinal))
        {
            profile = profile with
            {
                Email = userContext.Email.Trim(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            await profiles.SaveAsync(profile, cancellationToken);
            email = profile.Email;
        }

        return new CurrentUserResult(userContext.UserId, email, profile?.Role);
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
