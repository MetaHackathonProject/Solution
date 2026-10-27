namespace Hackathon.Infrastructure.Options;

public sealed class InterviewProgressOptions
{
    public const string SectionName = "InterviewProgress";

    public required string TableName { get; init; }

    public required string BucketName { get; init; }
}
