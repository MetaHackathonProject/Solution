namespace Hackathon.Infrastructure.Options;

public sealed class SolutionVisionReviewOptions
{
    public const string SectionName = "SolutionVisionReview";

    public required string StateMachineArn { get; init; }
}
