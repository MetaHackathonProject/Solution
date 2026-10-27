namespace Hackathon.Infrastructure.Options;

public sealed class InterviewSessionsOptions
{
    public const string SectionName = "InterviewSessions";

    public required string TableName { get; init; }
}
