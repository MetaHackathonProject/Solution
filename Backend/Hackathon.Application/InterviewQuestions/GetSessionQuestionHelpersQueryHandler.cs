using Hackathon.Application.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewQuestions;

public sealed class GetSessionQuestionHelpersQueryHandler(
    IInterviewSessionAccess access,
    IInterviewQuestionHelperStore questionHelpers)
    : IRequestHandler<GetSessionQuestionHelpersQuery, IReadOnlyList<QuestionHelper>?>
{
    public async Task<IReadOnlyList<QuestionHelper>?> Handle(
        GetSessionQuestionHelpersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var resolved = await access.ResolveAsync(request.SessionId, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        if (!resolved.CanSeeOverview)
        {
            return [];
        }

        return await questionHelpers.ListWithHelpersAsync(
            request.SessionId,
            cancellationToken);
    }
}