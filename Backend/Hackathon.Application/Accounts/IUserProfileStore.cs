using Hackathon.Domain.Accounts;

namespace Hackathon.Application.Accounts;

public interface IUserProfileStore
{
    Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken);

    Task SaveAsync(UserProfile profile, CancellationToken cancellationToken);
}
