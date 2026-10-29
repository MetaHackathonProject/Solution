using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.VoiceAgent;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.VoiceAgent;

public sealed class DynamoDbInterviewConversationStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<InterviewConversationsOptions> options) : IInterviewConversationStore
{
    public async Task SaveAsync(
        InterviewConversation conversation,
        CancellationToken cancellationToken)
    {
        await dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = options.Value.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(conversation.SessionId),
                    ["created_at"] = new() { N = conversation.CreatedAt.ToString() },
                    ["conversation_id"] = new(conversation.ConversationId),
                    ["purpose"] = new(conversation.Purpose),
                },
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<InterviewConversation>> ListBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var items = new List<InterviewConversation>();
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

    private static InterviewConversation FromItem(Dictionary<string, AttributeValue> item) =>
        new(
            item["session_id"].S,
            long.TryParse(item.GetValueOrDefault("created_at")?.N, out var createdAt) ? createdAt : 0,
            item.GetValueOrDefault("conversation_id")?.S ?? string.Empty,
            item.GetValueOrDefault("purpose")?.S ?? "interview");
}
