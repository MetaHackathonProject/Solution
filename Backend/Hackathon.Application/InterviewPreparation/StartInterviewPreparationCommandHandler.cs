using MediatR;

namespace Hackathon.Application.InterviewPreparation;

public sealed class StartInterviewPreparationCommandHandler
    : IRequestHandler<StartInterviewPreparationCommand, StartInterviewPreparationResult>
{
    public Task<StartInterviewPreparationResult> Handle(
        StartInterviewPreparationCommand request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new StartInterviewPreparationResult(string.Empty));
    }
}
