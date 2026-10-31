using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed record GetInterviewSessionQuery(string SessionId)
    : IRequest<InterviewSessionAccess?>;
