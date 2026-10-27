using System.Text.Json.Serialization;

namespace Hackathon.SeedInterviewProgress;

public sealed class SeedInterviewProgressRequest
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}
