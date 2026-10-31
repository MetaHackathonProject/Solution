namespace Hackathon.Infrastructure.Options;

public sealed class UserProfilesOptions
{
    public const string SectionName = "UserProfiles";

    public string TableName { get; init; } = string.Empty;
}
