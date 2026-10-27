using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewTasks;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewTasks;

public sealed class S3InteractiveAnswerStore(
    IAmazonS3 s3,
    IOptions<InterviewProgressOptions> options) : IInteractiveAnswerStore
{
    public async Task SaveAsync(
        string userId,
        string sessionId,
        string taskId,
        Stream content,
        string contentType,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        var extension = string.IsNullOrWhiteSpace(fileExtension)
            ? "bin"
            : fileExtension.TrimStart('.');

        var key = $"{userId}/{sessionId}/interactive-tasks/{taskId}/answer.{extension}";

        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);
    }
}