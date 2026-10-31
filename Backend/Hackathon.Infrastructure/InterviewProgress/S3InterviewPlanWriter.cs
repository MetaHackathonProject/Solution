using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewProgress;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewProgress;

public sealed class S3InterviewPlanWriter(
    IAmazonS3 s3,
    IOptions<InterviewProgressOptions> options) : IInterviewPlanWriter
{
    public async Task PutAsync(
        string userId,
        string sessionId,
        InterviewPlan plan,
        CancellationToken cancellationToken)
    {
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = $"{userId}/{sessionId}/plan.json",
                ContentBody = JsonSerializer.Serialize(plan),
                ContentType = "application/json",
            },
            cancellationToken);
    }
}
