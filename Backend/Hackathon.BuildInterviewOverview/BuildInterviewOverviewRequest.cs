using System.Text.Json.Serialization;

namespace Hackathon.BuildInterviewOverview;

public sealed class BuildInterviewOverviewRequest
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}
