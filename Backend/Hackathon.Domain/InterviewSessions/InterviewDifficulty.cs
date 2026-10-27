namespace Hackathon.Domain.InterviewSessions;

public static class InterviewDifficulty
{
    public const string Auto = "auto";
    public const string Easy = "easy";
    public const string Medium = "medium";
    public const string Hard = "hard";

    public static bool IsDefined(string value) =>
        value is Auto or Easy or Medium or Hard;
}
