using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Hackathon.Application.InterviewSessions;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewSessions;

public sealed class InterviewSessionArtifactStore(
    IAmazonDynamoDB dynamoDb,
    IAmazonS3 s3,
    IOptions<InterviewSessionsOptions> options) : IInterviewSessionArtifactStore
{
    private const int BatchSize = 25;

    public async Task DeleteAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await DeleteBySessionIdAsync(options.Value.QuestionsTableName, sessionId, cancellationToken);
        await DeleteBySessionIdAsync(options.Value.ProgressTableName, sessionId, cancellationToken);
        await DeleteS3PrefixAsync($"{userId}/{sessionId}/", cancellationToken);
    }

    private async Task DeleteBySessionIdAsync(
        string tableName,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var keys = await QueryKeysAsync(tableName, sessionId, cancellationToken);
        foreach (var chunk in keys.Chunk(BatchSize))
        {
            await WriteBatchAsync(
                tableName,
                chunk.Select(questionId => new WriteRequest
                {
                    DeleteRequest = new DeleteRequest
                    {
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["session_id"] = new(sessionId),
                            ["question_id"] = new(questionId),
                        },
                    },
                }),
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> QueryKeysAsync(
        string tableName,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var questionIds = new List<string>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = tableName,
                KeyConditionExpression = "session_id = :session_id",
                ProjectionExpression = "question_id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":session_id"] = new(sessionId),
                },
            };

            if (exclusiveStartKey is { Count: > 0 })
            {
                request.ExclusiveStartKey = exclusiveStartKey;
            }

            var response = await dynamoDb.QueryAsync(request, cancellationToken);
            if (response.Items is { Count: > 0 })
            {
                questionIds.AddRange(
                    response.Items
                        .Select(item => item.GetValueOrDefault("question_id")?.S)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id!));
            }

            exclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;
        }
        while (exclusiveStartKey is not null);

        return questionIds;
    }

    private async Task WriteBatchAsync(
        string tableName,
        IEnumerable<WriteRequest> requests,
        CancellationToken cancellationToken)
    {
        var requestItems = new Dictionary<string, List<WriteRequest>>
        {
            [tableName] = requests.ToList(),
        };

        while (requestItems.Count > 0)
        {
            var response = await dynamoDb.BatchWriteItemAsync(
                new BatchWriteItemRequest { RequestItems = requestItems },
                cancellationToken);

            requestItems = response.UnprocessedItems is { Count: > 0 }
                ? response.UnprocessedItems
                : [];
        }
    }

    private async Task DeleteS3PrefixAsync(string prefix, CancellationToken cancellationToken)
    {
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
