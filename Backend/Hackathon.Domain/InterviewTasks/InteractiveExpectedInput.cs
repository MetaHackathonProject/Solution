namespace Hackathon.Domain.InterviewTasks;

public static class InteractiveExpectedInput
{
    public static readonly IReadOnlyList<string> All =
    [
        "code",
        "diagram",
        "schema",
        "text",
        "free_string",
    ];

    public static bool IsDefined(string value) =>
        All.Contains(value, StringComparer.Ordinal);
}
