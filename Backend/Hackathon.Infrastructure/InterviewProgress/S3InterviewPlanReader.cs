using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewProgress;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewProgress;

public sealed class S3InterviewPlanReader(
    IAmazonS3 s3,
    IOptions<InterviewProgressOptions> options) : IInterviewPlanReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<InterviewPlan?> GetAsync(
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
                    Key = $"{userId}/{sessionId}/plan.json",
                },
                cancellationToken);

            await using var stream = response.ResponseStream;
            return await JsonSerializer.DeserializeAsync<InterviewPlan>(
                stream,
                SerializerOptions,
                cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
