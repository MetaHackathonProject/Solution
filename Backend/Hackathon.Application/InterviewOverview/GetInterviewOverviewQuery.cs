using MediatR;

namespace Hackathon.Application.InterviewOverview;

public sealed record GetInterviewOverviewQuery(string SessionId)
    : IRequest<InterviewResultOverview?>;
