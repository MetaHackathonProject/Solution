using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.InterviewProgress;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewProgress;

public sealed class DynamoDbInterviewProgressStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<InterviewProgressOptions> options) : IInterviewProgressStore
{
    private const int BatchSize = 25;

    public async Task ReplaceSessionAsync(
        string sessionId,
        IReadOnlyList<InterviewProgressItem> items,
        CancellationToken cancellationToken)
    {
        var existing = await QueryAllAsync(sessionId, cancellationToken);
        foreach (var chunk in existing.Chunk(BatchSize))
        {
            await WriteBatchAsync(
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

        foreach (var chunk in items.Chunk(BatchSize))
        {
            await WriteBatchAsync(
                chunk.Select(item => new WriteRequest
                {
                    PutRequest = new PutRequest
                    {
                        Item = ToItem(item),
                    },
                }),
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> QueryAllAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var questionIds = new List<string>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = options.Value.TableName,
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
        IEnumerable<WriteRequest> requests,
        CancellationToken cancellationToken)
    {
        var requestItems = new Dictionary<string, List<WriteRequest>>
        {
            [options.Value.TableName] = requests.ToList(),
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

    private static Dictionary<string, AttributeValue> ToItem(InterviewProgressItem item) =>
        new()
        {
            ["session_id"] = new(item.SessionId),
            ["question_id"] = new(item.QuestionId),
            ["module"] = new(item.Module),
            ["module_order"] = new() { N = item.ModuleOrder.ToString() },
            ["question_order"] = new() { N = item.QuestionOrder.ToString() },
            ["order_key"] = new(item.OrderKey),
            ["question_content"] = new(item.QuestionContent),
            ["module_objective"] = new(item.ModuleObjective),
            ["question_objective"] = new(item.QuestionObjective),
        };
}
