using MediatR;

namespace Hackathon.Application.InterviewOverview;

public sealed record BuildInterviewOverviewCommand(string UserId, string SessionId)
    : IRequest<BuildInterviewOverviewResult>;

public sealed record BuildInterviewOverviewResult(string OverviewStatus, int Score);
