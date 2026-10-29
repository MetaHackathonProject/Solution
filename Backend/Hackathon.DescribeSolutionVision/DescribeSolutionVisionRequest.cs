using System.Text.Json.Serialization;

namespace Hackathon.DescribeSolutionVision;

public sealed class DescribeSolutionVisionRequest
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    [JsonPropertyName("answer_key")]
    public required string AnswerKey { get; init; }
}
