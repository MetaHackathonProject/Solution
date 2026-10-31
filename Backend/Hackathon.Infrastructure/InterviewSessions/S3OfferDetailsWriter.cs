using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewSessions;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewSessions;

public sealed class S3OfferDetailsWriter(
    IAmazonS3 s3,
    IOptions<InterviewSessionsOptions> options) : IOfferDetailsWriter
{
    public async Task PutAsync(
        string userId,
        string sessionId,
        string title,
        string companyName,
        string overviewForVoiceAgent,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                title,
                company_name = companyName,
                overview_for_voice_agent = overviewForVoiceAgent,
            });

        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = $"{userId}/{sessionId}/offer/details.json",
                ContentBody = payload,
                ContentType = "application/json",
            },
            cancellationToken);
    }
}
