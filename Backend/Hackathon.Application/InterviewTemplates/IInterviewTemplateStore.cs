using Hackathon.Domain.InterviewTemplates;

namespace Hackathon.Application.InterviewTemplates;

public interface IInterviewTemplateStore
{
    Task SaveAsync(InterviewTemplate template, CancellationToken cancellationToken);

    Task<InterviewTemplate?> GetAsync(
        string employerUserId,
        string templateId,
        CancellationToken cancellationToken);

    Task<InterviewTemplate?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<InterviewTemplate>> ListByEmployerAsync(
        string employerUserId,
        CancellationToken cancellationToken);

    Task DeleteAsync(string employerUserId, string templateId, CancellationToken cancellationToken);
}
