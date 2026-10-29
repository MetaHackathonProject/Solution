using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hackathon.Application.InterviewOverview;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class MetaInterviewOverviewComposer(
    IHttpClientFactory httpClientFactory,
    IOptions<MetaModelApiOptions> options) : IInterviewOverviewComposer
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions OverviewJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public async Task<InterviewResultOverview> ComposeAsync(
        string transcriptJson,
        IReadOnlyList<InteractiveJudgment> judgments,
        string feedbackLanguage,
        CancellationToken cancellationToken)
    {
        var judgmentsJson = JsonSerializer.Serialize(judgments, OverviewJsonOptions);
        var language = string.IsNullOrWhiteSpace(feedbackLanguage) ? "en" : feedbackLanguage;
        var user =
            $"Write an interview overview in language '{language}'. Return ONLY a single valid JSON object (no markdown).\n" +
            "Keys: summary (string), score (1-10 int), questions (array of objects with question, answer, opinion, recommendations string array), " +
            "recommendations (string array), interactiveTasks (array of objects with taskId, kind, score, strengths, gaps, summary).\n" +
            "Rules: every string must be JSON-escaped (\\\" for quotes, \\\\ for backslash, \\n for newlines). " +
            "Keep each question/answer/opinion under 400 characters. Prefer ASCII quotes. " +
            "Use the transcript for spoken Q&A. Copy interactive judgment fields into interactiveTasks (do not invent scores). " +
            "Overall score should reflect spoken + interactive performance.\n\n" +
            $"Transcript JSON:\n{Truncate(transcriptJson, 18000)}\n\n" +
            $"Interactive judgments JSON:\n{Truncate(judgmentsJson, 8000)}";

        var raw = await SendChatAsync(
            "You are an interview debrief writer. Be specific, fair, and concise. Output one valid JSON object only.",
            user,
            cancellationToken);

        if (TryParseOverview(raw, judgments, out var overview))
        {
            return overview;
        }

        // Model often breaks JSON with unescaped quotes inside answers — ask it to repair.
        var broken = ExtractJsonObject(raw) ?? raw;
        var repairRaw = await SendChatAsync(
            "You fix broken JSON. Return ONLY one valid JSON object. Escape all string quotes properly.",
            "This JSON is invalid. Return a corrected version with the same schema " +
            "(summary, score, questions[{question,answer,opinion,recommendations}], recommendations, interactiveTasks). " +
            "If a string is corrupted, shorten it but keep valid JSON.\n\n" +
            Truncate(broken, 24000),
            cancellationToken);

        if (TryParseOverview(repairRaw, judgments, out overview))
        {
            return overview;
        }

        return BuildFallbackOverview(judgments, language);
    }

    private static bool TryParseOverview(
        string raw,
        IReadOnlyList<InteractiveJudgment> judgments,
        out InterviewResultOverview overview)
    {
        overview = null!;
        var candidates = EnumerateJsonCandidates(raw);
        foreach (var candidate in candidates)
        {
            try
            {
                var parsed = ParseOverviewLoose(candidate, judgments);
                if (parsed is not null)
                {
                    overview = Normalize(parsed, judgments);
                    return true;
                }
            }
            catch (JsonException)
            {
                // try next candidate / repair path
            }
        }

        return false;
    }

    private static InterviewResultOverview? ParseOverviewLoose(
        string json,
        IReadOnlyList<InteractiveJudgment> judgments)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var summary = ReadString(root, "summary") ?? string.Empty;
        var score = ReadInt(root, "score") ?? 0;
        var recommendations = ReadStringArray(root, "recommendations");
        var questions = ReadQuestions(root);
        var interactive = ReadInteractiveTasks(root);

        return new InterviewResultOverview(
            summary,
            score,
            questions,
            recommendations,
            interactive.Count > 0 ? interactive : judgments);
    }

    private static IReadOnlyList<OverviewQuestion> ReadQuestions(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "questions", out var questionsEl) ||
            questionsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<OverviewQuestion>();
        foreach (var item in questionsEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var question = ReadString(item, "question");
            if (string.IsNullOrWhiteSpace(question))
            {
                continue;
            }

            list.Add(new OverviewQuestion(
                question,
                ReadString(item, "answer") ?? string.Empty,
                ReadString(item, "opinion") ?? string.Empty,
                ReadStringArray(item, "recommendations")));
        }

        return list;
    }

    private static IReadOnlyList<InteractiveJudgment> ReadInteractiveTasks(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "interactiveTasks", out var tasksEl) &&
            !TryGetPropertyIgnoreCase(root, "interactive_tasks", out tasksEl))
        {
            return [];
        }

        if (tasksEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<InteractiveJudgment>();
        foreach (var item in tasksEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var taskId = ReadString(item, "taskId") ?? ReadString(item, "task_id");
            if (string.IsNullOrWhiteSpace(taskId))
            {
                continue;
            }

            var score = ReadInt(item, "score") ?? 1;
            list.Add(new InteractiveJudgment(
                taskId,
                ReadString(item, "kind") ?? "unknown",
                Math.Clamp(score, 1, 10),
                ReadStringArray(item, "strengths"),
                ReadStringArray(item, "gaps"),
                ReadString(item, "summary") ?? string.Empty));
        }

        return list;
    }

    private static InterviewResultOverview Normalize(
        InterviewResultOverview overview,
        IReadOnlyList<InteractiveJudgment> judgments)
    {
        overview = overview with
        {
            Summary = string.IsNullOrWhiteSpace(overview.Summary) ? string.Empty : overview.Summary,
            Questions = overview.Questions ?? Array.Empty<OverviewQuestion>(),
            Recommendations = overview.Recommendations ?? Array.Empty<string>(),
            InteractiveTasks = overview.InteractiveTasks is { Count: > 0 }
                ? overview.InteractiveTasks
                : judgments,
        };

        if (overview.Score is < 1 or > 10)
        {
            var blended = judgments.Count == 0
                ? 5
                : (int)Math.Round(judgments.Average(j => j.Score));
            overview = overview with { Score = Math.Clamp(blended, 1, 10) };
        }

        return overview;
    }

    private static InterviewResultOverview BuildFallbackOverview(
        IReadOnlyList<InteractiveJudgment> judgments,
        string language)
    {
        var score = judgments.Count == 0
            ? 5
            : (int)Math.Round(judgments.Average(j => j.Score));
        score = Math.Clamp(score, 1, 10);

        var summary = language.StartsWith("pl", StringComparison.OrdinalIgnoreCase)
            ? "Nie udało się w pełni złożyć szczegółowego podsumowania z transkryptu. Poniżej oceny zadań interaktywnych."
            : "A detailed spoken-answer overview could not be composed from the transcript. Interactive task judgments are included below.";

        return new InterviewResultOverview(
            summary,
            score,
            [],
            [],
            judgments);
    }

    private async Task<string> SendChatAsync(
        string system,
        string user,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new InvalidOperationException("Meta Model API key is not configured.");
        }

        var body = new ChatCompletionRequest(
            opts.ModelId,
            [
                new ChatMessage("system", system),
                new ChatMessage("user", user),
            ],
            new ResponseFormat("json_object"));

        var client = httpClientFactory.CreateClient("MetaModelApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: RequestJsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Some gateways reject response_format — retry once without it.
            if ((int)response.StatusCode is 400 or 422 && body.ResponseFormat is not null)
            {
                return await SendChatAsyncWithoutResponseFormat(system, user, cancellationToken);
            }

            throw new InvalidOperationException(
                $"Meta Model API returned {(int)response.StatusCode}: {Truncate(raw, 400)}");
        }

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
               ?? throw new InvalidOperationException("Empty model response.");
    }

    private async Task<string> SendChatAsyncWithoutResponseFormat(
        string system,
        string user,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var body = new ChatCompletionRequest(
            opts.ModelId,
            [
                new ChatMessage("system", system),
                new ChatMessage("user", user),
            ],
            null);

        var client = httpClientFactory.CreateClient("MetaModelApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: RequestJsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Meta Model API returned {(int)response.StatusCode}: {Truncate(raw, 400)}");
        }

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
               ?? throw new InvalidOperationException("Empty model response.");
    }

    private static IEnumerable<string> EnumerateJsonCandidates(string raw)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in new[]
                 {
                     ExtractJsonObject(raw),
                     StripMarkdownFence(raw),
                     raw.Trim(),
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
            {
                continue;
            }

            yield return candidate;

            var normalized = NormalizeCommonJsonIssues(candidate);
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return raw[start..(end + 1)];
    }

    private static string StripMarkdownFence(string raw)
    {
        var trimmed = raw.Trim();
        var match = Regex.Match(
            trimmed,
            "^```(?:json)?\\s*(.*?)\\s*```$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : trimmed;
    }

    private static string NormalizeCommonJsonIssues(string json)
    {
        // Trailing commas before } or ]
        var withoutTrailingCommas = Regex.Replace(json, ",\\s*([}\\]])", "$1");
        // Smart quotes → ASCII
        return withoutTrailingCommas
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'');
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement obj, string name) =>
        TryGetPropertyIgnoreCase(obj, name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int? ReadInt(JsonElement obj, string name)
    {
        if (!TryGetPropertyIgnoreCase(obj, name, out var el))
        {
            return null;
        }

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
        {
            return n;
        }

        if (el.ValueKind == JsonValueKind.String &&
            int.TryParse(el.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement obj, string name)
    {
        if (!TryGetPropertyIgnoreCase(obj, name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return el.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("response_format")] ResponseFormat? ResponseFormat);

    private sealed record ResponseFormat(
        [property: JsonPropertyName("type")] string Type);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] object Content);
}
