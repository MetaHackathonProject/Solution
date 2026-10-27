using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed record DeleteInterviewSessionCommand(string SessionId) : IRequest<bool>;
