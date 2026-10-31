using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.InterviewTemplates;
using Hackathon.Domain.InterviewTemplates;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.InterviewTemplates;

public sealed class DynamoDbInterviewTemplateStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<InterviewTemplatesOptions> options) : IInterviewTemplateStore
{
    public const string InviteCodeIndex = "invite_code_index";

    public Task SaveAsync(InterviewTemplate template, CancellationToken cancellationToken) =>
        dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = options.Value.TableName,
                Item = ToItem(template),
            },
            cancellationToken);

    public async Task<InterviewTemplate?> GetAsync(
        string employerUserId,
        string templateId,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(
            new GetItemRequest
            {
                TableName = options.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["employer_user_id"] = new(employerUserId),
                    ["template_id"] = new(templateId),
                },
            },
            cancellationToken);

        return response.Item is { Count: > 0 } ? FromItem(response.Item) : null;
    }

    public async Task<InterviewTemplate?> GetByInviteCodeAsync(
        string inviteCode,
        CancellationToken cancellationToken)
    {
        var response = await dynamoDb.QueryAsync(
            new QueryRequest
            {
                TableName = options.Value.TableName,
                IndexName = InviteCodeIndex,
                KeyConditionExpression = "invite_code = :invite_code",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":invite_code"] = new(inviteCode),
                },
                Limit = 1,
            },
            cancellationToken);

        return response.Items is { Count: > 0 } ? FromItem(response.Items[0]) : null;
    }

    public async Task<IReadOnlyList<InterviewTemplate>> ListByEmployerAsync(
        string employerUserId,
        CancellationToken cancellationToken)
    {
        var items = new List<InterviewTemplate>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = options.Value.TableName,
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

        return items.OrderByDescending(item => item.UpdatedAt).ToList();
    }

    public Task DeleteAsync(
        string employerUserId,
        string templateId,
        CancellationToken cancellationToken) =>
        dynamoDb.DeleteItemAsync(
            new DeleteItemRequest
            {
                TableName = options.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["employer_user_id"] = new(employerUserId),
                    ["template_id"] = new(templateId),
                },
            },
            cancellationToken);

    private static Dictionary<string, AttributeValue> ToItem(InterviewTemplate template)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["employer_user_id"] = new(template.EmployerUserId),
            ["template_id"] = new(template.TemplateId),
            ["name"] = new(template.Name),
            ["job_title"] = new(template.JobTitle),
            ["company"] = new(template.Company),
            ["language"] = new(template.Language),
            ["invite_code"] = new(template.InviteCode),
            ["definition"] = new(template.DefinitionJson),
            ["created_at"] = new() { N = template.CreatedAt.ToString() },
            ["updated_at"] = new() { N = template.UpdatedAt.ToString() },
        };

        if (!string.IsNullOrWhiteSpace(template.OfferUrl))
        {
            item["offer_url"] = new(template.OfferUrl);
        }

        return item;
    }

    private static InterviewTemplate FromItem(Dictionary<string, AttributeValue> item)
    {
        var offerUrl = item.GetValueOrDefault("offer_url")?.S;
        return new InterviewTemplate(
            item["employer_user_id"].S,
            item["template_id"].S,
            item.GetValueOrDefault("name")?.S ?? string.Empty,
            item.GetValueOrDefault("job_title")?.S ?? string.Empty,
            item.GetValueOrDefault("company")?.S ?? string.Empty,
            item.GetValueOrDefault("language")?.S ?? string.Empty,
            string.IsNullOrWhiteSpace(offerUrl) ? null : offerUrl,
            item.GetValueOrDefault("invite_code")?.S ?? string.Empty,
            item.GetValueOrDefault("definition")?.S ?? "{}",
            ParseLong(item.GetValueOrDefault("created_at")),
            ParseLong(item.GetValueOrDefault("updated_at")));
    }

    private static long ParseLong(AttributeValue? value) =>
        value?.N is { Length: > 0 } number && long.TryParse(number, out var parsed)
            ? parsed
            : 0;
}
