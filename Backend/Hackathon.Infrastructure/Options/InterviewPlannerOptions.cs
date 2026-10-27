namespace Hackathon.Infrastructure.Options;

public sealed class InterviewPlannerOptions
{
    public const string SectionName = "InterviewPlanner";

    public required string StateMachineArn { get; init; }
}
