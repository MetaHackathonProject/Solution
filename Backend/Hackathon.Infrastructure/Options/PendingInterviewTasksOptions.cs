namespace Hackathon.Infrastructure.Options;

public sealed class PendingInterviewTasksOptions
{
    public const string SectionName = "PendingInterviewTasks";

    public required string TableName { get; init; }
}
