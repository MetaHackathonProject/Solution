namespace Hackathon.Infrastructure.Options;

public sealed class ElevenLabsOptions
{
    public const string SectionName = "ElevenLabs";

    public required string ApiUrl { get; init; }

    public required string ApiKey { get; init; }

    public required string AgentId { get; init; }
}
