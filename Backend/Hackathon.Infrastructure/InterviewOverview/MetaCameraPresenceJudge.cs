using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hackathon.Application.InterviewOverview;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class MetaCameraPresenceJudge(
    IHttpClientFactory httpClientFactory,
    IOptions<MetaModelApiOptions> options) : ICameraPresenceJudge
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const long MaxVideoBytes = 200L * 1024 * 1024;

    public async Task<CameraPresenceReview?> JudgeAsync(
        IReadOnlyList<CameraVideoLink> videos,
        IReadOnlyList<CameraStill> stills,
        CancellationToken cancellationToken)
    {
        var clips = videos
            .Where(video => video.Size > 0 && video.Size <= MaxVideoBytes)
            .TakeLast(2)
            .ToList();

        if (clips.Count > 0)
        {
            try
            {
                return await CompleteAsync(BuildPrompt("video"), BuildVideoParts(clips), cancellationToken);
            }
            catch
            {
                // WebM or a fetch failure — fall back to stills.
            }
        }

        if (stills.Count == 0)
        {
            return null;
        }

        return await CompleteAsync(BuildPrompt("stills"), BuildStillParts(stills), cancellationToken);
    }

    private async Task<CameraPresenceReview> CompleteAsync(
        string userText,
        IReadOnlyList<object> mediaParts,
        CancellationToken cancellationToken)
    {
        var parts = new List<object> { new ChatTextPart("text", userText) };
        parts.AddRange(mediaParts);

        var body = new ChatCompletionRequest(
            options.Value.ModelId,
            [
                new ChatMessage(
                    "system",
                    "You review interview camera footage. Be conservative. Output JSON only."),
                new ChatMessage("user", parts),
            ]);

        var raw = await SendChatAsync(body, cancellationToken);
        return Parse(raw);
    }

    private static string BuildPrompt(string source) =>
        $"Review this candidate camera {source} from a mock interview. " +
        "Return ONLY JSON with keys: presence (visible|mostly_visible|not_visible|unknown), " +
        "mimicsSummary (short, non-clinical description of expression and engagement), " +
        "signals (string array of observable facts only), integrityRisk (0-100 int), " +
        "confidence (0-100 int), verdict (no_clear_risk|some_concerns|high_concern|inconclusive), " +
        "rationale (string).\n" +
        "Rules: never state that the candidate cheated as a fact. " +
        "integrityRisk is how concerning the visuals look; confidence is how sure you are given quality and coverage. " +
        "Empty, green, or no-face footage => presence unknown or not_visible, verdict inconclusive, integrityRisk 0, " +
        "confidence high that the video is unusable. " +
        "Possible signals are hypotheses only: second person, phone, reading off-screen, eyes off camera, covering lens.";

    private static IReadOnlyList<object> BuildVideoParts(IReadOnlyList<CameraVideoLink> videos) =>
        videos
            .Select(video => (object)new ChatVideoPart("video_url", new ChatVideoUrl(video.Url)))
            .ToList();

    private static IReadOnlyList<object> BuildStillParts(IReadOnlyList<CameraStill> stills) =>
        stills
            .Take(6)
            .Select(still =>
            {
                var mime = NormalizeMime(still.ContentType, still.Content);
                var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(still.Content)}";
                return (object)new ChatImagePart("image_url", new ChatImageUrl(dataUrl));
            })
            .ToList();

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

    private static CameraPresenceReview Parse(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return Inconclusive("Model did not return a usable camera review.");
        }

        using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
        var root = doc.RootElement;
        var presence = ReadString(root, "presence") ?? "unknown";
        var verdict = ReadString(root, "verdict") ?? "inconclusive";
        return new CameraPresenceReview(
            NormalizePresence(presence),
            ReadString(root, "mimicsSummary") ?? string.Empty,
            ReadStrings(root, "signals"),
            Clamp100(root, "integrityRisk"),
            Clamp100(root, "confidence"),
            NormalizeVerdict(verdict),
            ReadString(root, "rationale") ?? string.Empty);
    }

    private static CameraPresenceReview Inconclusive(string rationale) =>
        new("unknown", string.Empty, [], 0, 40, "inconclusive", rationale);

    private static string NormalizePresence(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "visible" => "visible",
            "mostly_visible" or "mostly-visible" => "mostly_visible",
            "not_visible" or "not-visible" => "not_visible",
            _ => "unknown",
        };

    private static string NormalizeVerdict(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "no_clear_risk" or "no-clear-risk" => "no_clear_risk",
            "some_concerns" or "some-concerns" => "some_concerns",
            "high_concern" or "high-concern" => "high_concern",
            _ => "inconclusive",
        };

    private static int Clamp100(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var el) && el.TryGetInt32(out var value))
        {
            return Math.Clamp(value, 0, 100);
        }

        return 0;
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) ? el.GetString() : null;

    private static IReadOnlyList<string> ReadStrings(JsonElement root, string name)
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

    private static string NormalizeMime(string contentType, byte[] content)
    {
        var type = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (type.StartsWith("image/", StringComparison.Ordinal))
        {
            return type == "image/jpg" ? "image/jpeg" : type;
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8)
        {
            return "image/jpeg";
        }

        return "image/jpeg";
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

    private sealed record ChatVideoPart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("video_url")] ChatVideoUrl VideoUrl);

    private sealed record ChatVideoUrl(
        [property: JsonPropertyName("url")] string Url);
}
