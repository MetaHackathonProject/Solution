namespace Hackathon.Infrastructure.Options;

public sealed class InterviewOverviewOptions
{
    public const string SectionName = "InterviewOverview";

    public string StateMachineArn { get; init; } = string.Empty;
}
