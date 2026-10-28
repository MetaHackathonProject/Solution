using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.InterviewTasks;
using Hackathon.Domain.InterviewTasks;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewTasks;

public sealed class DynamoDbPendingInterviewTaskStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<PendingInterviewTasksOptions> options) : IPendingInterviewTaskStore
{
    public async Task<PendingInterviewTask?> GetAsync(
        string sessionId,
        string taskId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(
            new GetItemRequest
            {
                TableName = options.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(sessionId),
                    ["task_id"] = new(taskId),
                },
            },
            cancellationToken);

        return response.Item is { Count: > 0 } ? FromItem(response.Item) : null;
    }

    public async Task<IReadOnlyList<PendingInterviewTask>> ListBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var items = new List<PendingInterviewTask>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = options.Value.TableName,
                KeyConditionExpression = "session_id = :session_id",
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
                items.AddRange(response.Items.Select(FromItem));
            }

            exclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;
        }
        while (exclusiveStartKey is not null);

        return items;
    }

    public async Task UpdateStatusAsync(
        string sessionId,
        string taskId,
        string status,
        CancellationToken cancellationToken)
    {
        await dynamoDb.UpdateItemAsync(
            new UpdateItemRequest
            {
                TableName = options.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(sessionId),
                    ["task_id"] = new(taskId),
                },
                UpdateExpression = "SET #status = :status",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status",
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new(status),
                },
            },
            cancellationToken);
    }

    public async Task SetVisionReviewAsync(
        string sessionId,
        string taskId,
        string visionReviewStatus,
        string? visionDescription,
        CancellationToken cancellationToken)
    {
        var update = "SET vision_review_status = :vision_status";
        var values = new Dictionary<string, AttributeValue>
        {
            [":vision_status"] = new(visionReviewStatus),
        };

        if (visionDescription is null)
        {
            update += " REMOVE vision_description";
        }
        else
        {
            update += ", vision_description = :vision_description";
            values[":vision_description"] = new(visionDescription);
        }

        await dynamoDb.UpdateItemAsync(
            new UpdateItemRequest
            {
                TableName = options.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(sessionId),
                    ["task_id"] = new(taskId),
                },
                UpdateExpression = update,
                ExpressionAttributeValues = values,
            },
            cancellationToken);
    }

    public async Task ResetAllToNotAskedAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var tasks = await ListBySessionAsync(sessionId, cancellationToken);
        foreach (var task in tasks)
        {
            await dynamoDb.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = options.Value.TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["session_id"] = new(sessionId),
                        ["task_id"] = new(task.TaskId),
                    },
                    UpdateExpression =
                        "SET #status = :status REMOVE vision_review_status, vision_description",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#status"] = "status",
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":status"] = new(InteractiveTaskStatus.NotAsked),
                    },
                },
                cancellationToken);
        }
    }

    public async Task<bool> HasOpenAsync(string sessionId, CancellationToken cancellationToken)
    {
        var tasks = await ListBySessionAsync(sessionId, cancellationToken);
        return tasks.Any(task =>
            task.Status is InteractiveTaskStatus.NotAsked or InteractiveTaskStatus.InProgress);
    }

    private static PendingInterviewTask FromItem(Dictionary<string, AttributeValue> item) =>
        new(
            item["session_id"].S,
            item["task_id"].S,
            item.GetValueOrDefault("module")?.S ?? string.Empty,
            item.GetValueOrDefault("question_content_formatted")?.S ?? string.Empty,
            item.GetValueOrDefault("question_content")?.S ?? string.Empty,
            item.GetValueOrDefault("status")?.S ?? InteractiveTaskStatus.NotAsked,
            item.GetValueOrDefault("expected_input")?.S ?? string.Empty,
            item.GetValueOrDefault("assessment_goal")?.S,
            item.GetValueOrDefault("vision_review_status")?.S,
            item.GetValueOrDefault("vision_description")?.S);
}
