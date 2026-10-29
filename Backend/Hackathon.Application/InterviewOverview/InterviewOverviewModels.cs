using System.Text.Json.Serialization;

namespace Hackathon.Application.InterviewOverview;

public sealed record InteractiveJudgment(
    [property: JsonPropertyName("taskId")] string TaskId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("strengths")] IReadOnlyList<string> Strengths,
    [property: JsonPropertyName("gaps")] IReadOnlyList<string> Gaps,
    [property: JsonPropertyName("summary")] string Summary);

public sealed record OverviewQuestion(
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("opinion")] string Opinion,
    [property: JsonPropertyName("recommendations")] IReadOnlyList<string> Recommendations);

public sealed record InterviewResultOverview(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("questions")] IReadOnlyList<OverviewQuestion> Questions,
    [property: JsonPropertyName("recommendations")] IReadOnlyList<string> Recommendations,
    [property: JsonPropertyName("interactiveTasks")] IReadOnlyList<InteractiveJudgment> InteractiveTasks);
