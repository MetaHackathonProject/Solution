namespace Hackathon.Domain.InterviewTasks;

public static class ExpectedInput
{
    public const string Code = "code";
    public const string Diagram = "diagram";
    public const string Schema = "schema";
    public const string Text = "text";
    public const string FreeString = "free_string";

    public static bool IsKnown(string value) =>
        value is Code or Diagram or Schema or Text or FreeString;
}
