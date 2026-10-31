using System.Text.Json;
using System.Text.Json.Serialization;
using Hackathon.Domain.InterviewSessions;
using Hackathon.Domain.InterviewTasks;

namespace Hackathon.Application.InterviewTemplates;

public sealed class InterviewTemplateDefinition
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonPropertyName("spokenQuestions")]
    public IReadOnlyList<TemplateSpokenQuestion> SpokenQuestions { get; init; } = [];

    [JsonPropertyName("interactiveTasks")]
    public IReadOnlyList<TemplateInteractiveTask> InteractiveTasks { get; init; } = [];

    public static InterviewTemplateDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Template definition is required.");
        }

        var definition = JsonSerializer.Deserialize<InterviewTemplateDefinition>(json, SerializerOptions)
            ?? throw new ArgumentException("Template definition is required.");
        definition.Validate();
        return definition;
    }

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public void Validate()
    {
        if (SpokenQuestions is not { Count: > 0 })
        {
            throw new ArgumentException("Add at least one spoken question.");
        }

        foreach (var question in SpokenQuestions)
        {
            if (string.IsNullOrWhiteSpace(question.Question))
            {
                throw new ArgumentException("Every spoken question needs question text.");
            }

            if (string.IsNullOrWhiteSpace(question.AssessmentGoal))
            {
                throw new ArgumentException("Every spoken question needs an assessment goal.");
            }

            if (!InterviewModules.IsDefined(question.Module))
            {
                throw new ArgumentException($"Unknown module '{question.Module}'.");
            }
        }

        foreach (var task in InteractiveTasks)
        {
            if (string.IsNullOrWhiteSpace(task.QuestionContent))
            {
                throw new ArgumentException("Every interactive task needs question content.");
            }

            if (string.IsNullOrWhiteSpace(task.AssessmentGoal))
            {
                throw new ArgumentException("Every interactive task needs an assessment goal.");
            }

            if (!InterviewModules.IsDefined(task.Module))
            {
                throw new ArgumentException($"Unknown module '{task.Module}'.");
            }

            if (!InteractiveExpectedInput.IsDefined(task.ExpectedInput))
            {
                throw new ArgumentException(
                    "expectedInput must be code, diagram, schema, text, or free_string.");
            }
        }
    }
}

public sealed class TemplateSpokenQuestion
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("module")]
    public required string Module { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }

    [JsonPropertyName("assessmentGoal")]
    public required string AssessmentGoal { get; init; }
}

public sealed class TemplateInteractiveTask
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("module")]
    public required string Module { get; init; }

    [JsonPropertyName("questionContent")]
    public required string QuestionContent { get; init; }

    [JsonPropertyName("questionContentFormatted")]
    public string? QuestionContentFormatted { get; init; }

    [JsonPropertyName("expectedInput")]
    public required string ExpectedInput { get; init; }

    [JsonPropertyName("assessmentGoal")]
    public required string AssessmentGoal { get; init; }
}
