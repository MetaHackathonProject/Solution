using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Hackathon.Application.Accounts;
using Hackathon.Domain.Accounts;
using Hackathon.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Hackathon.Infrastructure.Accounts;

public sealed class DynamoDbUserProfileStore(
    IAmazonDynamoDB dynamoDb,
    IOptions<UserProfilesOptions> options) : IUserProfileStore
{
    public async Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(
            new GetItemRequest
            {
                TableName = options.Value.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["user_id"] = new(userId),
                },
            },
            cancellationToken);

        return response.Item is { Count: > 0 } ? FromItem(response.Item) : null;
    }

    public Task SaveAsync(UserProfile profile, CancellationToken cancellationToken) =>
        dynamoDb.PutItemAsync(
            new PutItemRequest
            {
                TableName = options.Value.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["user_id"] = new(profile.UserId),
                    ["role"] = new(profile.Role),
                    ["created_at"] = new() { N = profile.CreatedAt.ToString() },
                    ["updated_at"] = new() { N = profile.UpdatedAt.ToString() },
                    ["email"] = new(profile.Email),
                },
            },
            cancellationToken);

    private static UserProfile FromItem(Dictionary<string, AttributeValue> item) =>
        new(
            item["user_id"].S,
            item.GetValueOrDefault("role")?.S ?? string.Empty,
            ParseLong(item.GetValueOrDefault("created_at")),
            ParseLong(item.GetValueOrDefault("updated_at")),
            item.GetValueOrDefault("email")?.S ?? string.Empty);

    private static long ParseLong(AttributeValue? value) =>
        value?.N is { Length: > 0 } number && long.TryParse(number, out var parsed)
            ? parsed
            : 0;
}
