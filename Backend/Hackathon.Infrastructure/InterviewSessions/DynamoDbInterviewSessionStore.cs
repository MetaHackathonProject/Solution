using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewSessions;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewSessions;

public sealed class DynamoDbInterviewSessionStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<InterviewSessionsOptions> options) : IInterviewSessionStore
{
    public async Task SaveAsync(InterviewSession session, CancellationToken cancellationToken)
    {
        await dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = options.Value.TableName,
                Item = ToItem(session),
            },
            cancellationToken);
    }

    public async Task<InterviewSession?> GetAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(
            new GetItemRequest
            {
                TableName = options.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["user_id"] = new(userId),
                    ["session_id"] = new(sessionId),
                },
            },
            cancellationToken);

        return response.Item.Count == 0 ? null : FromItem(response.Item);
    }

    public async Task<IReadOnlyList<InterviewSession>> ListByUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var sessions = new List<InterviewSession>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var response = await dynamoDb.QueryAsync(
                new QueryRequest
                {
                    TableName = options.Value.TableName,
                    KeyConditionExpression = "user_id = :user_id",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":user_id"] = new(userId),
                    },
                    ExclusiveStartKey = exclusiveStartKey,
                },
                cancellationToken);

            sessions.AddRange(response.Items.Select(FromItem));
            exclusiveStartKey = response.LastEvaluatedKey.Count == 0 ? null : response.LastEvaluatedKey;
        }
        while (exclusiveStartKey is not null);

        return sessions
            .OrderByDescending(session => session.CreatedAt)
            .ToList();
    }

    private static Dictionary<string, AttributeValue> ToItem(InterviewSession session) =>
        new()
        {
            ["user_id"] = new(session.UserId),
            ["session_id"] = new(session.SessionId),
            ["name"] = new(session.Name),
            ["offer_url"] = new(session.OfferUrl ?? string.Empty),
            ["recruiter_language"] = new(session.Settings.RecruiterLanguage),
            ["difficulty"] = new(session.Settings.Difficulty),
            ["status"] = new(session.Status),
            ["created_at"] = new() { N = session.CreatedAt.ToString() },
            ["updated_at"] = new() { N = session.UpdatedAt.ToString() },
        };

    private static InterviewSession FromItem(Dictionary<string, AttributeValue> item)
    {
        var offerUrl = item.GetValueOrDefault("offer_url")?.S;
        return new InterviewSession(
            item["user_id"].S,
            item["session_id"].S,
            item.GetValueOrDefault("name")?.S ?? string.Empty,
            string.IsNullOrWhiteSpace(offerUrl) ? null : offerUrl,
            new InterviewSessionSettings(
                item.GetValueOrDefault("recruiter_language")?.S ?? string.Empty,
                item.GetValueOrDefault("difficulty")?.S ?? InterviewDifficulty.Auto),
            item.GetValueOrDefault("status")?.S ?? InterviewSessionStatus.Started,
            ParseLong(item.GetValueOrDefault("created_at")),
            ParseLong(item.GetValueOrDefault("updated_at")));
    }

    private static long ParseLong(AttributeValue? value) =>
        value?.N is { Length: > 0 } number && long.TryParse(number, out var parsed)
            ? parsed
            : 0;
}
