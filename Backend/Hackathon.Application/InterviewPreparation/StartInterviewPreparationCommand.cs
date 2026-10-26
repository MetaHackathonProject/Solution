using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewPreparation;

public sealed record StartInterviewPreparationCommand(
    string Name,
    string? OfferUrl,
    InterviewSessionSettings Settings) : IRequest<StartInterviewPreparationResult>;

public sealed record StartInterviewPreparationResult(string SessionId);
