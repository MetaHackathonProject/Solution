namespace Hackathon.Domain.InterviewTasks;

public static class VisionReviewStatus
{
    public const string Skipped = "Skipped";
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Ready = "Ready";
    public const string Failed = "Failed";

    public static bool IsTerminal(string? value) =>
        value is Ready or Failed or Skipped;

    public static bool IsInFlight(string? value) =>
        value is Pending or Running;
}
