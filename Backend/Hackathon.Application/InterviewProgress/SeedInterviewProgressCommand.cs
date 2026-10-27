using MediatR;

namespace Hackathon.Application.InterviewProgress;

public sealed record SeedInterviewProgressCommand(string UserId, string SessionId)
    : IRequest<SeedInterviewProgressResult>;

public sealed record SeedInterviewProgressResult(int QuestionsSeeded);
