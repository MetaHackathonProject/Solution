using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using MediatR;

namespace Hackathon.Application.VoiceAgent;

public sealed class GetVoiceAgentContextQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IOfferDetailsReader offerDetailsReader)
    : IRequestHandler<GetVoiceAgentContextQuery, VoiceAgentContextResult?>
{
    private const int JobTitleMaxLength = 120;
    private const int CompanyMaxLength = 120;
    private const int JobOverviewMaxLength = 1500;

    public async Task<VoiceAgentContextResult?> Handle(
        GetVoiceAgentContextQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var session = await sessionStore.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);

        if (session is null)
        {
            return null;
        }

        var offer = await offerDetailsReader.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);

        return new VoiceAgentContextResult(
            Normalize(offer?.Title, JobTitleMaxLength),
            Normalize(offer?.CompanyName, CompanyMaxLength),
            Normalize(offer?.OverviewForVoiceAgent, JobOverviewMaxLength),
            string.IsNullOrWhiteSpace(session.Settings.RecruiterLanguage)
                ? null
                : session.Settings.RecruiterLanguage.Trim());
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength].TrimEnd();
    }
}
