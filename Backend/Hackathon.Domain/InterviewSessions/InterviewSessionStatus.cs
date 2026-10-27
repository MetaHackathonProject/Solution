namespace Hackathon.Domain.InterviewSessions;

public static class InterviewSessionStatus
{
    public const string Started = "STARTED";
    public const string ReadingOffer = "READING_OFFER";
    public const string GeneratingQuestions = "GENERATING_QUESTIONS";
    public const string GeneratingPlan = "GENERATING_PLAN";
    public const string Success = "SUCCESS";
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}
