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
                Item = new Dictionary<string, AttributeValue>
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
                },
            },
            cancellationToken);
    }
}
