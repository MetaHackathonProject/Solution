namespace Hackathon.Domain.InterviewTemplates;

public sealed record InterviewTemplate(
    string EmployerUserId,
    string TemplateId,
    string Name,
    string JobTitle,
    string Company,
    string Language,
    string? OfferUrl,
    string InviteCode,
    string DefinitionJson,
    long CreatedAt,
    long UpdatedAt);
