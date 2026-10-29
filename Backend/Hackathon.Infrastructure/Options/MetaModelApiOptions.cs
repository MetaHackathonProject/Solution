namespace Hackathon.Infrastructure.Options;

public sealed class MetaModelApiOptions
{
    public const string SectionName = "MetaModelApi";

    public string ApiUrl { get; init; } = "https://api.meta.ai/v1";

    public string ModelId { get; init; } = "muse-spark-1.3";

    /// <summary>Required for DescribeSolutionVision; unused by the API Lambda.</summary>
    public string ApiKey { get; init; } = string.Empty;
}
