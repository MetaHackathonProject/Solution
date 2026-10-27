using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewSessions;

public sealed record ListInterviewSessionsQuery
    : IRequest<IReadOnlyList<InterviewSession>>;
