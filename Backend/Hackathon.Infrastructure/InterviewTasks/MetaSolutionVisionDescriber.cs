using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hackathon.Application.InterviewTasks;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewTasks;

public sealed class MetaSolutionVisionDescriber(
    IHttpClientFactory httpClientFactory,
    IOptions<MetaModelApiOptions> options) : ISolutionVisionDescriber
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string> DescribeAsync(
        byte[] content,
        string contentType,
        string taskPrompt,
        string expectedInput,
        string module,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new InvalidOperationException("Meta Model API key is not configured.");
        }

        var mime = NormalizeMime(contentType, content);
        var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(content)}";

        var systemPrompt =
            "You describe a candidate's interactive interview submission for a voice walkthrough interviewer. " +
            "Write clear plain text only: layout, labels, boxes, arrows, entities, relationships, and notable gaps vs the exercise. " +
            "Do not invent content that is not visible. Keep under 8000 characters. No markdown fences.";

        var userText = new StringBuilder();
        userText.AppendLine("Describe this submitted visual answer for an interviewer.");
        if (!string.IsNullOrWhiteSpace(module))
        {
            userText.AppendLine($"Module: {module}");
        }

        if (!string.IsNullOrWhiteSpace(expectedInput))
        {
            userText.AppendLine($"Expected input type: {expectedInput}");
        }

        if (!string.IsNullOrWhiteSpace(taskPrompt))
        {
            userText.AppendLine();
            userText.AppendLine("Exercise prompt:");
            userText.AppendLine(taskPrompt.Length > 4000 ? taskPrompt[..4000] + "…" : taskPrompt);
        }

        var body = new ChatCompletionRequest(
            opts.ModelId,
            [
                new ChatMessage("system", systemPrompt),
                new ChatMessage(
                    "user",
                    new object[]
                    {
                        new ChatTextPart("text", userText.ToString()),
                        new ChatImagePart("image_url", new ChatImageUrl(dataUrl)),
                    }),
            ]);

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
                $"Meta Model API returned {(int)response.StatusCode}: {Truncate(raw, 500)}");
        }

        using var doc = JsonDocument.Parse(raw);
        var description = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException("Meta Model API returned an empty description.");
        }

        return description;
    }

    private static string NormalizeMime(string contentType, byte[] content)
    {
        var type = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (type.StartsWith("image/", StringComparison.Ordinal) || type is "application/pdf")
        {
            return type;
        }

        // PNG magic
        if (content.Length >= 4 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
        {
            return "image/png";
        }

        // JPEG magic
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // PDF magic
        if (content.Length >= 4 && content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46)
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
