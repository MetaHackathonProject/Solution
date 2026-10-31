using Hackathon.Domain.InterviewTemplates;

namespace Hackathon.Application.InterviewTemplates;

public interface IInterviewAssignmentStore
{
    Task SaveAsync(InterviewAssignment assignment, CancellationToken cancellationToken);

    Task<InterviewAssignment?> GetBySessionAsync(string sessionId, CancellationToken cancellationToken);

    Task<InterviewAssignment?> GetByCandidateTemplateAsync(
        string candidateUserId,
        string templateId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InterviewAssignment>> ListByEmployerAsync(
        string employerUserId,
        CancellationToken cancellationToken);
}
