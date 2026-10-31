using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.InterviewTemplates;
using Hackathon.Domain.InterviewTemplates;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewTemplates;

public sealed class DynamoDbInterviewAssignmentStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<InterviewTemplatesOptions> options) : IInterviewAssignmentStore
{
    public const string EmployerIndex = "employer_user_id_index";
    public const string CandidateTemplateIndex = "candidate_template_index";

    public Task SaveAsync(InterviewAssignment assignment, CancellationToken cancellationToken) =>
        dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = options.Value.AssignmentsTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(assignment.SessionId),
                    ["employer_user_id"] = new(assignment.EmployerUserId),
                    ["candidate_user_id"] = new(assignment.CandidateUserId),
                    ["template_id"] = new(assignment.TemplateId),
                    ["template_name"] = new(assignment.TemplateName),
                    ["job_title"] = new(assignment.JobTitle),
                    ["company"] = new(assignment.Company),
                    ["created_at"] = new() { N = assignment.CreatedAt.ToString() },
                },
            },
            cancellationToken);

    public async Task<InterviewAssignment?> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(
            new GetItemRequest
            {
                TableName = options.Value.AssignmentsTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["session_id"] = new(sessionId),
                },
            },
            cancellationToken);

        return response.Item is { Count: > 0 } ? FromItem(response.Item) : null;
    }

    public async Task<InterviewAssignment?> GetByCandidateTemplateAsync(
        string candidateUserId,
        string templateId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(
            new QueryRequest
            {
                TableName = options.Value.AssignmentsTableName,
                IndexName = CandidateTemplateIndex,
                KeyConditionExpression = "candidate_user_id = :candidate_user_id AND template_id = :template_id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":candidate_user_id"] = new(candidateUserId),
                    [":template_id"] = new(templateId),
                },
                Limit = 1,
            },
            cancellationToken);

        return response.Items is { Count: > 0 } ? FromItem(response.Items[0]) : null;
    }

    public async Task<IReadOnlyList<InterviewAssignment>> ListByEmployerAsync(
        string employerUserId,
        CancellationToken cancellationToken)
    {
        var items = new List<InterviewAssignment>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = options.Value.AssignmentsTableName,
                IndexName = EmployerIndex,
                KeyConditionExpression = "employer_user_id = :employer_user_id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":employer_user_id"] = new(employerUserId),
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

    private static InterviewAssignment FromItem(Dictionary<string, AttributeValue> item) =>
        new(
            item["session_id"].S,
            item.GetValueOrDefault("employer_user_id")?.S ?? string.Empty,
            item.GetValueOrDefault("candidate_user_id")?.S ?? string.Empty,
            item.GetValueOrDefault("template_id")?.S ?? string.Empty,
            item.GetValueOrDefault("template_name")?.S ?? string.Empty,
            item.GetValueOrDefault("job_title")?.S ?? string.Empty,
            item.GetValueOrDefault("company")?.S ?? string.Empty,
            ParseLong(item.GetValueOrDefault("created_at")));

    private static long ParseLong(AttributeValue? value) =>
        value?.N is { Length: > 0 } number && long.TryParse(number, out var parsed)
            ? parsed
            : 0;
}
