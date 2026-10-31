namespace Hackathon.Infrastructure.Options;

public sealed class InterviewTemplatesOptions
{
    public const string SectionName = "InterviewTemplates";

    public string TableName { get; init; } = string.Empty;

    public string AssignmentsTableName { get; init; } = string.Empty;
}
