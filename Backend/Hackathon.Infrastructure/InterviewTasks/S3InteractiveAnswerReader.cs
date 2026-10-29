using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewTasks;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewTasks;

public sealed class S3InteractiveAnswerReader(
    IAmazonS3 s3,
    IOptions<InterviewProgressOptions> options) : IInteractiveAnswerReader
{
    public async Task<InteractiveAnswerObject?> GetByKeyAsync(
        string answerKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(answerKey);

        try
        {
            using var response = await s3.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = options.Value.BucketName,
                    Key = answerKey.TrimStart('/'),
                },
                cancellationToken);

            await using var stream = response.ResponseStream;
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);

            var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                ? "application/octet-stream"
                : response.Headers.ContentType;

            return new InteractiveAnswerObject(memory.ToArray(), contentType, answerKey);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
