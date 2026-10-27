using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Hackathon.Application.VoiceAgent;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.VoiceAgent;

public sealed class ElevenLabsConversationClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ElevenLabsOptions> options) : IElevenLabsConversationClient
{
    public async Task<ElevenLabsSignedConversation> GetSignedUrlAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.AgentId))
        {
            throw new InvalidOperationException("ElevenLabs ApiKey and AgentId must be configured.");
        }

        var client = httpClientFactory.CreateClient("ElevenLabs");
        var path =
            $"/v1/convai/conversation/get-signed-url?agent_id={Uri.EscapeDataString(config.AgentId)}&include_conversation_id=true";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("xi-api-key", config.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SignedUrlResponse>(cancellationToken)
            ?? throw new InvalidOperationException("ElevenLabs signed URL response was empty.");

        if (string.IsNullOrWhiteSpace(body.SignedUrl))
        {
            throw new InvalidOperationException("ElevenLabs signed URL was missing.");
        }

        var conversationId = !string.IsNullOrWhiteSpace(body.ConversationId)
            ? body.ConversationId
            : ExtractConversationId(body.SignedUrl);

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new InvalidOperationException("ElevenLabs conversation_id was missing.");
        }

        return new ElevenLabsSignedConversation(body.SignedUrl, conversationId);
    }

    private static string? ExtractConversationId(string signedUrl)
    {
        foreach (var part in new Uri(signedUrl).Query.TrimStart('?')
                     .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2
                && string.Equals(pair[0], "conversation_id", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }

        return null;
    }

    private sealed class SignedUrlResponse
    {
        [JsonPropertyName("signed_url")]
        public string? SignedUrl { get; init; }

        [JsonPropertyName("conversation_id")]
        public string? ConversationId { get; init; }
    }
}
