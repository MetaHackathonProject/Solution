using System.Text.Json.Serialization;

namespace Hackathon.Application.InterviewProgress;

public sealed class InterviewPlan
{
    [JsonPropertyName("estimated_duration_minutes")]
    public int EstimatedDurationMinutes { get; init; }

    [JsonPropertyName("sections")]
    public required IReadOnlyList<InterviewPlanSection> Sections { get; init; }
}

public sealed class InterviewPlanSection
{
    [JsonPropertyName("module")]
    public required string Module { get; init; }

    [JsonPropertyName("estimated_duration_minutes")]
    public int EstimatedDurationMinutes { get; init; }

    [JsonPropertyName("objective")]
    public required string Objective { get; init; }

    [JsonPropertyName("module_order")]
    public int ModuleOrder { get; init; }

    [JsonPropertyName("questions")]
    public required IReadOnlyList<InterviewPlanQuestion> Questions { get; init; }
}

public sealed class InterviewPlanQuestion
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }

    [JsonPropertyName("assessment_goal")]
    public required string AssessmentGoal { get; init; }

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; init; }

    public bool IsInteractive =>
        string.Equals(Type, "interactive", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(TaskId);
}
