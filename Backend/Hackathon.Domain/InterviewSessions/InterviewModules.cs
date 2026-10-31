namespace Hackathon.Domain.InterviewSessions;

public static class InterviewModules
{
    public static readonly IReadOnlyList<string> All =
    [
        "Communication",
        "TechnicalKnowledge",
        "PracticalSkills",
        "ArchitectureAndDesign",
        "ProblemSolving",
        "ExperienceValidation",
        "DomainKnowledge",
        "Behavioral",
        "ForeignLanguage",
    ];

    public static bool IsDefined(string value) =>
        All.Contains(value, StringComparer.Ordinal);
}
