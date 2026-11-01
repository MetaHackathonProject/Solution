namespace Hackathon.Domain.InterviewTemplates;

public sealed record InterviewAssignment(
    string SessionId,
    string EmployerUserId,
    string CandidateUserId,
    string TemplateId,
    string TemplateName,
    string JobTitle,
    string Company,
    long CreatedAt,
    string CandidateEmail = "");
