using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hackathon.Application.InterviewOverview;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class MetaInteractiveSolutionJudge(
    IHttpClientFactory httpClientFactory,
    IOptions<MetaModelApiOptions> options) : IInteractiveSolutionJudge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions JudgmentJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Task<InteractiveJudgment> JudgeVisualAsync(
        byte[] content,
        string contentType,
        string taskId,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken) =>
        JudgeMultimodalAsync(
            options.Value.ModelId,
            content,
            contentType,
            taskId,
            "visual",
            taskPrompt,
            expectedInput,
            module,
            cancellationToken);

    public async Task<InteractiveJudgment> JudgeCodeAsync(
        byte[] zipContent,
        string taskId,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken)
    {
        var sourceText = ExtractZipSources(zipContent);
        var prompt =
            "Judge this coding interview submission. Return ONLY JSON with keys: " +
            "task_id, kind (\"code\"), score (1-10 int), strengths (string array), gaps (string array), summary (string).\n" +
            $"task_id={taskId}\nmodule={module}\nexpected_input={expectedInput}\n\n" +
            $"Exercise:\n{Truncate(taskPrompt, 4000)}\n\n" +
            $"Submission:\n{Truncate(sourceText, 12000)}";

        var raw = await CompleteTextAsync(
            options.Value.WonderModelId,
            "You are a strict but fair coding interview reviewer. Output JSON only.",
            prompt,
            cancellationToken);

        return ParseJudgment(raw, taskId, "code");
    }

    public async Task<InteractiveJudgment> JudgeTextAsync(
        string text,
        string taskId,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken)
    {
        var prompt =
            "Judge this written interview submission. Return ONLY JSON with keys: " +
            "task_id, kind (\"text\"), score (1-10 int), strengths (string array), gaps (string array), summary (string).\n" +
            $"task_id={taskId}\nmodule={module}\nexpected_input={expectedInput}\n\n" +
            $"Exercise:\n{Truncate(taskPrompt, 4000)}\n\n" +
            $"Submission:\n{Truncate(text, 12000)}";

        var raw = await CompleteTextAsync(
            options.Value.ModelId,
            "You are a strict but fair interview reviewer for written answers. Output JSON only.",
            prompt,
            cancellationToken);

        return ParseJudgment(raw, taskId, "text");
    }

    private async Task<InteractiveJudgment> JudgeMultimodalAsync(
        string modelId,
        byte[] content,
        string contentType,
        string taskId,
        string kind,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken)
    {
        var mime = NormalizeMime(contentType, content);
        var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(content)}";
        var text =
            "Judge this visual interview submission. Return ONLY JSON with keys: " +
            "task_id, kind (\"visual\"), score (1-10 int), strengths (string array), gaps (string array), summary (string).\n" +
            $"task_id={taskId}\nmodule={module}\nexpected_input={expectedInput}\n\n" +
            $"Exercise:\n{Truncate(taskPrompt, 4000)}";

        var body = new ChatCompletionRequest(
            modelId,
            [
                new ChatMessage("system", "You are a strict but fair interview reviewer for diagrams/schemas. Output JSON only."),
                new ChatMessage(
                    "user",
                    new object[]
                    {
                        new ChatTextPart("text", text),
                        new ChatImagePart("image_url", new ChatImageUrl(dataUrl)),
                    }),
            ]);

        var raw = await SendChatAsync(body, cancellationToken);
        return ParseJudgment(raw, taskId, kind);
    }

    private async Task<string> CompleteTextAsync(
        string modelId,
        string system,
        string user,
        CancellationToken cancellationToken)
    {
        var body = new ChatCompletionRequest(
            modelId,
            [
                new ChatMessage("system", system),
                new ChatMessage("user", user),
            ]);
        return await SendChatAsync(body, cancellationToken);
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

    private static InteractiveJudgment ParseJudgment(string raw, string taskId, string kind)
    {
        var json = ExtractJsonObject(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var score = root.TryGetProperty("score", out var scoreEl) && scoreEl.TryGetInt32(out var s)
            ? Math.Clamp(s, 1, 10)
            : 1;
        var strengths = ReadStringArray(root, "strengths");
        var gaps = ReadStringArray(root, "gaps");
        var summary = root.TryGetProperty("summary", out var summaryEl)
            ? summaryEl.GetString() ?? string.Empty
            : string.Empty;

        return new InteractiveJudgment(taskId, kind, score, strengths, gaps, summary);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return el.EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
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

    private static string ExtractZipSources(byte[] zipContent)
    {
        using var stream = new MemoryStream(zipContent);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var builder = new StringBuilder();
        foreach (var entry in archive.Entries.OrderBy(e => e.FullName))
        {
            if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (entry.Length > 400_000)
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            if (text.Contains('\0'))
            {
                continue;
            }

            builder.AppendLine($"===== {entry.FullName} =====");
            builder.AppendLine(text.TrimEnd());
            builder.AppendLine();
        }

        return builder.Length == 0 ? "(empty archive)" : builder.ToString();
    }

    private static string NormalizeMime(string contentType, byte[] content)
    {
        var type = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (type.StartsWith("image/", StringComparison.Ordinal) || type is "application/pdf")
        {
            return type;
        }

        if (content.Length >= 4 && content[0] == 0x89 && content[1] == 0x50)
        {
            return "image/png";
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8)
        {
            return "image/jpeg";
        }

        if (content.Length >= 4 && content[0] == 0x25 && content[1] == 0x50)
        {
            return "application/pdf";
        }

        return "image/png";
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] object Content);

    private sealed record ChatTextPart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ChatImagePart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("image_url")] ChatImageUrl ImageUrl);

    private sealed record ChatImageUrl(
        [property: JsonPropertyName("url")] string Url);
}
