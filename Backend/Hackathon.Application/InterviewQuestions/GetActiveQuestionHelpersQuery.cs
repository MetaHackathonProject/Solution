using MediatR;

namespace Hackathon.Application.InterviewQuestions;

public sealed record GetActiveQuestionHelpersQuery(string SessionId)
    : IRequest<IReadOnlyList<QuestionHelper>?>;
