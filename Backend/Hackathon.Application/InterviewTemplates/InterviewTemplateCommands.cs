using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed record CreateInterviewTemplateCommand(
    string Name,
    string JobTitle,
    string Company,
    string Language,
    string? OfferUrl,
    InterviewTemplateDefinition Definition) : IRequest<InterviewTemplateResult>;

public sealed record UpdateInterviewTemplateCommand(
    string TemplateId,
    string Name,
    string JobTitle,
    string Company,
    string Language,
    string? OfferUrl,
    InterviewTemplateDefinition Definition) : IRequest<InterviewTemplateResult?>;

public sealed record GetInterviewTemplateQuery(string TemplateId) : IRequest<InterviewTemplateResult?>;

public sealed record ListInterviewTemplatesQuery : IRequest<IReadOnlyList<InterviewTemplateResult>>;

public sealed record DeleteInterviewTemplateCommand(string TemplateId) : IRequest<bool>;

public sealed record GetInvitePreviewQuery(string InviteCode) : IRequest<InterviewInvitePreview?>;

public sealed record JoinInterviewTemplateCommand(string InviteCode) : IRequest<JoinInterviewTemplateResult>;

public sealed record ListEmployerAssignmentsQuery : IRequest<IReadOnlyList<EmployerAssignmentResult>>;

public sealed record ImportOfferFromUrlCommand(string OfferUrl) : IRequest<ImportedOfferResult>;

public sealed record StartEmployerOfferGenerationCommand(string OfferUrl, string Language)
    : IRequest<StartEmployerOfferGenerationResult>;

public sealed record GetGeneratedTemplateFromOfferQuery(string SessionId)
    : IRequest<GeneratedTemplateFromOfferResult?>;

public sealed record InterviewTemplateResult(
    string TemplateId,
    string Name,
    string JobTitle,
    string Company,
    string Language,
    string? OfferUrl,
    string InviteCode,
    InterviewTemplateDefinition Definition,
    long CreatedAt,
    long UpdatedAt);

public sealed record InterviewInvitePreview(
    string InviteCode,
    string Name,
    string JobTitle,
    string Company,
    string Language);

public sealed record JoinInterviewTemplateResult(string SessionId, bool AlreadyJoined);

public sealed record EmployerAssignmentResult(
    string SessionId,
    string TemplateId,
    string TemplateName,
    string JobTitle,
    string Company,
    string CandidateUserId,
    string CandidateEmail,
    string Status,
    string OverviewStatus,
    long CreatedAt,
    int? Score = null);

public sealed record ImportedOfferResult(string? Title, string? Company, string OfferUrl);

public sealed record StartEmployerOfferGenerationResult(string SessionId);

public sealed record GeneratedTemplateFromOfferResult(
    string SessionId,
    string Status,
    string? Title,
    string? Company,
    string OfferUrl,
    IReadOnlyList<TemplateSpokenQuestion> SpokenQuestions,
    IReadOnlyList<TemplateInteractiveTask> InteractiveTasks);
