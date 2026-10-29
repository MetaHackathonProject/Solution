using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewOverview;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewOverview;

public sealed class S3InterviewOverviewArtifactStore(
    IAmazonS3 s3,
    IOptions<InterviewProgressOptions> options) : IInterviewOverviewArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public Task PutTranscriptAsync(
        string userId,
        string sessionId,
        string transcriptJson,
        CancellationToken cancellationToken) =>
        PutTextAsync($"{userId}/{sessionId}/transcript/raw.txt", transcriptJson, "application/json", cancellationToken);

    public Task PutJudgmentAsync(
        string userId,
        string sessionId,
        string taskId,
        InteractiveJudgment judgment,
        CancellationToken cancellationToken) =>
        PutTextAsync(
            $"{userId}/{sessionId}/interactive-tasks/{taskId}/judgment.json",
            JsonSerializer.Serialize(judgment, JsonOptions),
            "application/json",
            cancellationToken);

    public async Task<InteractiveJudgment?> GetJudgmentAsync(
        string userId,
        string sessionId,
        string taskId,
        CancellationToken cancellationToken)
    {
        var text = await GetTextAsync(
            $"{userId}/{sessionId}/interactive-tasks/{taskId}/judgment.json",
            cancellationToken);
        return text is null
            ? null
            : JsonSerializer.Deserialize<InteractiveJudgment>(text, JsonOptions);
    }

    public Task PutOverviewAsync(
        string userId,
        string sessionId,
        InterviewResultOverview overview,
        CancellationToken cancellationToken) =>
        PutTextAsync(
            $"{userId}/{sessionId}/overview.json",
            JsonSerializer.Serialize(overview, JsonOptions),
            "application/json",
            cancellationToken);

    public async Task<InterviewResultOverview?> GetOverviewAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var text = await GetTextAsync($"{userId}/{sessionId}/overview.json", cancellationToken);
        return text is null
            ? null
            : JsonSerializer.Deserialize<InterviewResultOverview>(text, JsonOptions);
    }

    public Task<string?> GetTranscriptAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken) =>
        GetTextAsync($"{userId}/{sessionId}/transcript/raw.txt", cancellationToken);

    private async Task PutTextAsync(
        string key,
        string body,
        string contentType,
        CancellationToken cancellationToken)
    {
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = key,
                ContentBody = body,
                ContentType = contentType,
            },
            cancellationToken);
    }

    private async Task<string?> GetTextAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await s3.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = options.Value.BucketName,
                    Key = key,
                },
                cancellationToken);
            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
