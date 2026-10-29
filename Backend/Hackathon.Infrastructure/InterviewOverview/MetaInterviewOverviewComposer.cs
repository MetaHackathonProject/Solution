using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hackathon.Application.InterviewOverview;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class MetaInterviewOverviewComposer(
    IHttpClientFactory httpClientFactory,
    IOptions<MetaModelApiOptions> options) : IInterviewOverviewComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions OverviewJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
            $"Write an interview overview in language '{language}'. Return ONLY JSON with keys:\n" +
            "summary (string), score (1-10 int overall performance), questions (array of {question, answer, opinion, recommendations[]}), " +
            "recommendations (string array), interactiveTasks (array of {taskId, kind, score, strengths[], gaps[], summary}).\n" +
            "Use the transcript for spoken Q&A. Copy interactive judgment fields into interactiveTasks (do not invent scores).\n" +
            "Overall score should reflect spoken + interactive performance.\n\n" +
            $"Transcript JSON:\n{Truncate(transcriptJson, 20000)}\n\n" +
            $"Interactive judgments JSON:\n{Truncate(judgmentsJson, 8000)}";

        var body = new ChatCompletionRequest(
            options.Value.ModelId,
            [
                new ChatMessage(
                    "system",
                    "You are an interview debrief writer. Be specific, fair, and concise. Output JSON only."),
                new ChatMessage("user", user),
            ]);

        var raw = await SendChatAsync(body, cancellationToken);
        var json = ExtractJsonObject(raw);
        var overview = JsonSerializer.Deserialize<InterviewResultOverview>(json, OverviewJsonOptions)
                       ?? throw new InvalidOperationException("Could not parse overview JSON.");

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

    private async Task<string> SendChatAsync(ChatCompletionRequest body, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new InvalidOperationException("Meta Model API key is not configured.");
        }

        var client = httpClientFactory.CreateClient("MetaModelApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
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

    private static string ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("Model did not return a JSON object.");
        }

        return raw[start..(end + 1)];
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] object Content);
}
