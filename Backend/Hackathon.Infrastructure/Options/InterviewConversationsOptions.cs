namespace Hackathon.Infrastructure.Options;

public sealed class InterviewConversationsOptions
{
    public const string SectionName = "InterviewConversations";

    public string TableName { get; init; } = string.Empty;
}
