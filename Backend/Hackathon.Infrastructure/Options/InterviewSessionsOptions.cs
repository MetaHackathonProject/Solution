namespace Hackathon.Infrastructure.Options;

public sealed class InterviewSessionsOptions
{
    public const string SectionName = "InterviewSessions";

    public string TableName { get; init; } = string.Empty;

    public string QuestionsTableName { get; init; } = string.Empty;

    public string ProgressTableName { get; init; } = string.Empty;

    public string BucketName { get; init; } = string.Empty;
}
