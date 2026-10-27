using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewSessions;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewSessions;

public sealed class S3OfferDetailsReader(
    IAmazonS3 s3,
    IOptions<InterviewSessionsOptions> options) : IOfferDetailsReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<OfferDetails?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = options.Value.BucketName,
                    Key = $"{userId}/{sessionId}/offer/details.json",
                },
                cancellationToken);

            await using var stream = response.ResponseStream;
            var payload = await JsonSerializer.DeserializeAsync<OfferDetailsPayload>(
                stream,
                SerializerOptions,
                cancellationToken);

            return payload is null
                ? null
                : new OfferDetails(payload.Title, payload.CompanyName, payload.OverviewForVoiceAgent);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private sealed class OfferDetailsPayload
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; init; }

        [JsonPropertyName("overview_for_voice_agent")]
        public string? OverviewForVoiceAgent { get; init; }
    }
}
