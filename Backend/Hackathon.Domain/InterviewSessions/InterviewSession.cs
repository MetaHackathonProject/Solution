namespace Hackathon.Domain.InterviewSessions;

public sealed record InterviewSession(
    string UserId,
    string SessionId,
    string Name,
    string? OfferUrl,
    InterviewSessionSettings Settings,
    string Status,
    long CreatedAt,
    long UpdatedAt,
    string OverviewStatus = OverviewStatus.NotAvailable,
    string Source = InterviewSessionSource.Self,
    string? EmployerUserId = null,
    string? TemplateId = null,
    bool RetakeAllowed = true,
    bool HideCameraOverview = false);
