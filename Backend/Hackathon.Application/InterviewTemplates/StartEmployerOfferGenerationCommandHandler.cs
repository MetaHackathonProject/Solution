using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Application.InterviewPreparation;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.Accounts;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class StartEmployerOfferGenerationCommandHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewSessionStore sessionStore,
    IInterviewPreparationStarter preparationStarter)
    : IRequestHandler<StartEmployerOfferGenerationCommand, StartEmployerOfferGenerationResult>
{
    public async Task<StartEmployerOfferGenerationResult> Handle(
        StartEmployerOfferGenerationCommand request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        var offerUrl = request.OfferUrl.Trim();
        if (string.IsNullOrWhiteSpace(offerUrl))
        {
            throw new ArgumentException("Offer URL is required to generate questions.");
        }

        var language = string.IsNullOrWhiteSpace(request.Language) ? "English" : request.Language.Trim();
        var sessionId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await sessionStore.SaveAsync(
            new InterviewSession(
                userContext.UserId,
                sessionId,
                "Template draft",
                offerUrl,
                new InterviewSessionSettings(language, InterviewDifficulty.Auto),
                InterviewSessionStatus.Started,
                now,
                now,
                OverviewStatus.NotAvailable,
                InterviewSessionSource.TemplateDraft,
                RetakeAllowed: false,
                HideCameraOverview: true),
            cancellationToken);

        await preparationStarter.StartAsync(
            userContext.UserId,
            sessionId,
            offerUrl,
            language,
            InterviewDifficulty.Auto,
            cancellationToken);

        return new StartEmployerOfferGenerationResult(sessionId);
    }
}
