using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.InterviewQuestions;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewQuestions;

public sealed class DynamoDbInterviewQuestionHelperStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<InterviewSessionsOptions> options) : IInterviewQuestionHelperStore
{
    public const string ActiveHelpersQuestionId = "__active_helpers__";

    public async Task SetActiveQuestionIdsAsync(
        string sessionId,
        IReadOnlyList<string> questionIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = options.Value.QuestionsTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(sessionId),
                    ["question_id"] = new(ActiveHelpersQuestionId),
                    ["module"] = new("System"),
                    ["active_question_ids"] = new()
                    {
                        L = questionIds
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct(StringComparer.Ordinal)
                            .Select(id => new AttributeValue(id))
                            .ToList(),
                    },
                    ["updated_at"] = new() { N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
                },
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetActiveQuestionIdsAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var response = await dynamoDb.GetItemAsync(
            new GetItemRequest
            {
                TableName = options.Value.QuestionsTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(sessionId),
                    ["question_id"] = new(ActiveHelpersQuestionId),
                },
                ConsistentRead = true,
            },
            cancellationToken);

        if (response.Item is not { Count: > 0 })
        {
            return [];
        }

        if (!response.Item.TryGetValue("active_question_ids", out var ids) ||
            ids.L is not { Count: > 0 })
        {
            return [];
        }

        return ids.L
            .Select(value => value.S)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();
    }

    public Task ClearActiveQuestionIdsAsync(
        string sessionId,
        CancellationToken cancellationToken) =>
        SetActiveQuestionIdsAsync(sessionId, [], cancellationToken);

    public async Task<IReadOnlyList<QuestionHelper>> GetByIdsAsync(
        string sessionId,
        IReadOnlyList<string> questionIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var helpers = new List<QuestionHelper>();
        foreach (var questionId in questionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
        {
            if (questionId == ActiveHelpersQuestionId)
            {
                continue;
            }

            var response = await dynamoDb.GetItemAsync(
                new GetItemRequest
                {
                    TableName = options.Value.QuestionsTableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["session_id"] = new(sessionId),
                        ["question_id"] = new(questionId),
                    },
                },
                cancellationToken);

            var helper = FromItem(response.Item);
            if (helper is not null)
            {
                helpers.Add(helper);
            }
        }

        return helpers;
    }

    public async Task<IReadOnlyList<QuestionHelper>> ListWithHelpersAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var helpers = new List<QuestionHelper>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = options.Value.QuestionsTableName,
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
                helpers.AddRange(
                    response.Items
                        .Select(FromItem)
                        .Where(helper => helper is not null)
                        .Select(helper => helper!));
            }

            exclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;
        }
        while (exclusiveStartKey is not null);

        return helpers;
    }

    private static QuestionHelper? FromItem(Dictionary<string, AttributeValue>? item)
    {
        if (item is null || item.Count == 0)
        {
            return null;
        }

        var questionId = item.GetValueOrDefault("question_id")?.S;
        if (string.IsNullOrWhiteSpace(questionId) ||
            questionId == ActiveHelpersQuestionId)
        {
            return null;
        }

        var suggested = item.GetValueOrDefault("suggested_answer")?.S;
        if (string.IsNullOrWhiteSpace(suggested))
        {
            return null;
        }

        var hints = Array.Empty<string>();
        if (item.TryGetValue("hints", out var hintsAttr) && hintsAttr.L is { Count: > 0 })
        {
            hints = hintsAttr.L
                .Select(value => value.S)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!)
                .ToArray();
        }
        else if (item.TryGetValue("hints", out var hintsSs) && hintsSs.SS is { Count: > 0 })
        {
            hints = hintsSs.SS
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
        }

        return new QuestionHelper(
            questionId,
            item.GetValueOrDefault("module")?.S ?? string.Empty,
            item.GetValueOrDefault("question")?.S ?? string.Empty,
            suggested,
            hints);
    }
}
