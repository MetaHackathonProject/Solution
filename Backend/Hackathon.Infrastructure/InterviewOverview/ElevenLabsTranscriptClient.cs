using System.Text.Json;
using Hackathon.Application.InterviewOverview;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class ElevenLabsTranscriptClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ElevenLabsOptions> options) : IElevenLabsTranscriptClient
{
    public async Task<IReadOnlyList<TranscriptTurn>> GetTranscriptAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var client = httpClientFactory.CreateClient("ElevenLabs");
        Exception? lastError = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(4 * attempt, 15)), cancellationToken);
            }

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/v1/convai/conversations/{Uri.EscapeDataString(conversationId)}");
                request.Headers.Add("xi-api-key", options.Value.ApiKey);

                using var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!doc.RootElement.TryGetProperty("transcript", out var transcript) ||
                    transcript.ValueKind != JsonValueKind.Array)
                {
                    lastError = new InvalidOperationException("ElevenLabs transcript is not ready yet.");
                    continue;
                }

                var turns = new List<TranscriptTurn>();
                foreach (var item in transcript.EnumerateArray())
                {
                    var role = item.TryGetProperty("role", out var roleEl) ? roleEl.GetString() : null;
                    var message = item.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }

                    turns.Add(new TranscriptTurn(role, message));
                }

                return turns;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("Could not load ElevenLabs transcript.");
    }
}
