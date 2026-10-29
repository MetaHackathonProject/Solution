using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewQuestions;

public sealed class GetSessionQuestionHelpersQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewQuestionHelperStore questionHelpers)
    : IRequestHandler<GetSessionQuestionHelpersQuery, IReadOnlyList<QuestionHelper>?>
{
    public async Task<IReadOnlyList<QuestionHelper>?> Handle(
        GetSessionQuestionHelpersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var session = await sessionStore.GetAsync(
            userContext.UserId,
            request.SessionId,
            cancellationToken);

        if (session is null)
        {
            return null;
        }

        return await questionHelpers.ListWithHelpersAsync(
            request.SessionId,
            cancellationToken);
    }
}