using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewQuestions;

public sealed class GetActiveQuestionHelpersQueryHandler(
    IUserContext userContext,
    IInterviewSessionStore sessionStore,
    IInterviewQuestionHelperStore questionHelpers)
    : IRequestHandler<GetActiveQuestionHelpersQuery, IReadOnlyList<QuestionHelper>?>
{
    public async Task<IReadOnlyList<QuestionHelper>?> Handle(
        GetActiveQuestionHelpersQuery request,
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

        var activeIds = await questionHelpers.GetActiveQuestionIdsAsync(
            request.SessionId,
            cancellationToken);

        if (activeIds.Count == 0)
        {
            return [];
        }

        return await questionHelpers.GetByIdsAsync(
            request.SessionId,
            activeIds,
            cancellationToken);
    }
}
