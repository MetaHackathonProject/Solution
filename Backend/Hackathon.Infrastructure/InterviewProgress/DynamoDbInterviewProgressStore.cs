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
    private const string OrderKeyIndexName = "order-key-index";
    private const int BatchSize = 25;

    public async Task ReplaceSessionAsync(
        string sessionId,
        IReadOnlyList<InterviewProgressItem> items,
        CancellationToken cancellationToken)
    {
        var existing = await QueryQuestionIdsAsync(sessionId, cancellationToken);
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

    public async Task<bool> HasAnyAsync(string sessionId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(
            new QueryRequest
            {
                TableName = options.Value.TableName,
                KeyConditionExpression = "session_id = :session_id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":session_id"] = new(sessionId),
                },
                Limit = 1,
                Select = Select.COUNT,
            },
            cancellationToken);

        return response.Count > 0;
    }

    public async Task<PopInterviewProgressResult> PopNextAsync(
        string sessionId,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var ordered = await QueryOrderedAsync(sessionId, cancellationToken);
        if (ordered.Count == 0)
        {
            return new PopInterviewProgressResult([], 0);
        }

        var toPop = ordered.Take(count).ToList();
        foreach (var chunk in toPop.Chunk(BatchSize))
        {
            await WriteBatchAsync(
                chunk.Select(item => new WriteRequest
                {
                    DeleteRequest = new DeleteRequest
                    {
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["session_id"] = new(item.SessionId),
                            ["question_id"] = new(item.QuestionId),
                        },
                    },
                }),
                cancellationToken);
        }

        return new PopInterviewProgressResult(
            toPop,
            Math.Max(0, ordered.Count - toPop.Count));
    }

    private async Task<IReadOnlyList<string>> QueryQuestionIdsAsync(
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

    private async Task<IReadOnlyList<InterviewProgressItem>> QueryOrderedAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var items = new List<InterviewProgressItem>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = options.Value.TableName,
                IndexName = OrderKeyIndexName,
                KeyConditionExpression = "session_id = :session_id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":session_id"] = new(sessionId),
                },
                ScanIndexForward = true,
            };

            if (exclusiveStartKey is { Count: > 0 })
            {
                request.ExclusiveStartKey = exclusiveStartKey;
            }

            var response = await dynamoDb.QueryAsync(request, cancellationToken);
            if (response.Items is { Count: > 0 })
            {
                items.AddRange(response.Items.Select(FromItem));
            }

            exclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;
        }
        while (exclusiveStartKey is not null);

        return items;
    }

    private static InterviewProgressItem FromItem(Dictionary<string, AttributeValue> item) =>
        new(
            item["session_id"].S,
            item["question_id"].S,
            item.GetValueOrDefault("module")?.S ?? string.Empty,
            ParseInt(item.GetValueOrDefault("module_order")),
            ParseInt(item.GetValueOrDefault("question_order")),
            item.GetValueOrDefault("order_key")?.S ?? string.Empty,
            item.GetValueOrDefault("question_content")?.S ?? string.Empty,
            item.GetValueOrDefault("module_objective")?.S ?? string.Empty,
            item.GetValueOrDefault("question_objective")?.S ?? string.Empty);

    private static int ParseInt(AttributeValue? value) =>
        value?.N is { Length: > 0 } number && int.TryParse(number, out var parsed)
            ? parsed
            : 0;

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
