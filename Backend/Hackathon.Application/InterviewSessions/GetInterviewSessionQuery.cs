using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed record GetInterviewSessionQuery(string SessionId)
    : IRequest<InterviewSession?>;
