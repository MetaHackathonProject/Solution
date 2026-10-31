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

    public string CreateDownloadUrl(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        return s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = options.Value.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(25),
            Protocol = Protocol.HTTPS,
        });
    }

    public async Task<IReadOnlyList<RecordingVideoObject>> ListVideosAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var prefix = $"{userId}/{sessionId}/recording/segments/";
        var videos = new List<RecordingVideoObject>();
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

            foreach (var obj in listed.S3Objects ?? [])
            {
                var type = obj.Key.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                    ? "video/mp4"
                    : "video/webm";
                videos.Add(new RecordingVideoObject(obj.Key, type, obj.Size ?? 0));
            }

            continuationToken = listed.IsTruncated == true ? listed.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return videos
            .OrderBy(video => video.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<RecordingFrameObject>> ListFramesAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var prefix = $"{userId}/{sessionId}/recording/frames/";
        var frames = new List<RecordingFrameObject>();
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

            foreach (var obj in listed.S3Objects ?? [])
            {
                if (frames.Count >= 8)
                {
                    break;
                }

                using var response = await s3.GetObjectAsync(
                    options.Value.BucketName,
                    obj.Key,
                    cancellationToken);
                await using var memory = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memory, cancellationToken);
                var type = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                    ? "image/jpeg"
                    : response.Headers.ContentType;
                frames.Add(new RecordingFrameObject(obj.Key, memory.ToArray(), type));
            }

            continuationToken = listed.IsTruncated == true && frames.Count < 8
                ? listed.NextContinuationToken
                : null;
        }
        while (continuationToken is not null);

        return frames;
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
