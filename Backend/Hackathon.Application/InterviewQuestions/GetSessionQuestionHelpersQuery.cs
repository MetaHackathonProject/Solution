using MediatR;

namespace Hackathon.Application.InterviewQuestions;

public sealed record GetSessionQuestionHelpersQuery(string SessionId)
    : IRequest<IReadOnlyList<QuestionHelper>?>;
