using MediatR;

namespace Hackathon.Application.InterviewProgress;

public sealed class PopNextInterviewQuestionsCommandHandler(IInterviewProgressStore progressStore)
    : IRequestHandler<PopNextInterviewQuestionsCommand, PopNextInterviewQuestionsResult>
{
    private const int QuestionsToPop = 2;

    public async Task<PopNextInterviewQuestionsResult> Handle(
        PopNextInterviewQuestionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);

        var popped = await progressStore.PopNextAsync(
            request.SessionId,
            QuestionsToPop,
            cancellationToken);

        return new PopNextInterviewQuestionsResult(
            popped.Questions
                .Select(item => new InterviewQuestionResultItem(
                    item.QuestionContent,
                    item.QuestionObjective,
                    item.ModuleObjective,
                    item.Module))
                .ToList(),
            popped.QuestionsRemaining);
    }
}
