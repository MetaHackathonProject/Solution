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

    [JsonPropertyName("jobFacts")]
    public InterviewTemplateJobFacts? JobFacts { get; init; }

    public InterviewTemplateJobFacts ResolvedJobFacts() => JobFacts ?? new();

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

        ResolvedJobFacts().Validate();
    }
}

public sealed class InterviewTemplateJobFacts
{
    public static readonly string[] WorkTypes = ["Remote", "Hybrid", "On-site"];
    public static readonly string[] ContractTypes = ["Full-time", "Part-time", "Contract", "Internship"];

    [JsonPropertyName("workType")]
    public string? WorkType { get; init; }

    [JsonPropertyName("contractType")]
    public string? ContractType { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("salary")]
    public string? Salary { get; init; }

    [JsonPropertyName("benefits")]
    public IReadOnlyList<string> Benefits { get; init; } = [];

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    public InterviewTemplateJobFacts Normalized() =>
        new()
        {
            WorkType = NormalizeChoice(WorkType, WorkTypes),
            ContractType = NormalizeChoice(ContractType, ContractTypes),
            Location = Truncate(Location, 160),
            Salary = Truncate(Salary, 160),
            Benefits = Benefits
                .Select(benefit => Truncate(benefit, 80))
                .Where(benefit => benefit is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList(),
            Notes = Truncate(Notes, 800),
        };

    public void Validate()
    {
        var normalized = Normalized();
        if (WorkType is { Length: > 0 } && normalized.WorkType is null)
        {
            throw new ArgumentException("Work type must be Remote, Hybrid, or On-site.");
        }

        if (ContractType is { Length: > 0 } && normalized.ContractType is null)
        {
            throw new ArgumentException("Contract type must be Full-time, Part-time, Contract, or Internship.");
        }
    }

    public string ToVoiceOverviewLines()
    {
        var facts = Normalized();
        var parts = new List<string>();
        if (facts.WorkType is not null)
        {
            parts.Add($"Work type: {facts.WorkType}.");
        }

        if (facts.ContractType is not null)
        {
            parts.Add($"Contract: {facts.ContractType}.");
        }

        if (facts.Location is not null)
        {
            parts.Add($"Location: {facts.Location}.");
        }

        if (facts.Salary is not null)
        {
            parts.Add($"Salary: {facts.Salary}.");
        }

        if (facts.Benefits.Count > 0)
        {
            parts.Add($"Benefits: {string.Join("; ", facts.Benefits)}.");
        }

        if (facts.Notes is not null)
        {
            parts.Add(facts.Notes);
        }

        return string.Join(" ", parts);
    }

    private static string? NormalizeChoice(string? value, IReadOnlyList<string> allowed)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return allowed.FirstOrDefault(option =>
            string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd();
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
