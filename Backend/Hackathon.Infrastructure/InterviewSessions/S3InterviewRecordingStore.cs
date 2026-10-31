using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewSessions;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewSessions;

public sealed class S3InterviewRecordingStore(
    IAmazonS3 s3,
    IOptions<InterviewProgressOptions> options) : IInterviewRecordingStore
{
    public string CreateUploadUrl(string userId, string sessionId, string contentType, string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        return s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = options.Value.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(20),
            ContentType = contentType,
            Protocol = Protocol.HTTPS,
        });
    }

    public async Task DeleteBySessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var prefix = $"{userId}/{sessionId}/recording/";
        string? continuationToken = null;

        do
        {
            var listed = await s3.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = options.Value.BucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                },
                cancellationToken);

            if (listed.S3Objects is { Count: > 0 })
            {
                await s3.DeleteObjectsAsync(
                    new DeleteObjectsRequest
                    {
                        BucketName = options.Value.BucketName,
                        Objects = listed.S3Objects
                            .Select(obj => new KeyVersion { Key = obj.Key })
                            .ToList(),
                    },
                    cancellationToken);
            }

            continuationToken = listed.IsTruncated == true ? listed.NextContinuationToken : null;
        }
        while (continuationToken is not null);
    }
}
