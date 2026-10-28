using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed record RetakeInterviewSessionCommand(string SessionId)
    : IRequest<RetakeInterviewSessionResult?>;

public sealed record RetakeInterviewSessionResult(string SessionId, int QuestionsSeeded);
