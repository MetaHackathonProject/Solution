namespace Hackathon.Domain.Accounts;

public sealed record UserProfile(
    string UserId,
    string Role,
    long CreatedAt,
    long UpdatedAt,
    string Email = "");
