using MediatR;

namespace Hackathon.Application.VoiceAgent;

public sealed record GetVoiceAgentContextQuery(string SessionId)
    : IRequest<VoiceAgentContextResult?>;

public sealed record VoiceAgentContextResult(
    string? JobTitle,
    string? Company,
    string? JobOverview,
    string? Language);
