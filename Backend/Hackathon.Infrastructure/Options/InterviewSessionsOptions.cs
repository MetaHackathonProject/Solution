namespace Hackathon.Infrastructure.Options;

public sealed class InterviewSessionsOptions
{
    public const string SectionName = "InterviewSessions";

    public required string TableName { get; init; }

    public required string QuestionsTableName { get; init; }

    public required string ProgressTableName { get; init; }

    public required string BucketName { get; init; }
}
