using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewPreparation;

public sealed class StartInterviewPreparationCommandHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewPreparationStarter preparationStarter)
    : IRequestHandler<StartInterviewPreparationCommand, StartInterviewPreparationResult>
{
    public async Task<StartInterviewPreparationResult> Handle(
        StartInterviewPreparationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OfferUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Settings.Difficulty);
        if (!InterviewDifficulty.IsDefined(request.Settings.Difficulty))
        {
            throw new ArgumentException(
                "Difficulty must be auto, easy, medium, or hard.",
                nameof(request));
        }

        var sessionId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await sessionStore.SaveAsync(
            new InterviewSession(
                userContext.UserId,
                sessionId,
                request.Name,
                request.OfferUrl,
                request.Settings,
                InterviewSessionStatus.Started,
                now,
                now),
            cancellationToken);

        await preparationStarter.StartAsync(
            userContext.UserId,
            sessionId,
            request.OfferUrl,
            request.Settings.RecruiterLanguage,
            request.Settings.Difficulty,
            cancellationToken);

        return new StartInterviewPreparationResult(sessionId);
    }
}
