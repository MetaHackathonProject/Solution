using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Application.InterviewSessions;
using Hackathon.Domain.Accounts;
using Hackathon.Domain.InterviewSessions;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class ListEmployerAssignmentsQueryHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewAssignmentStore assignments,
    IInterviewSessionStore sessions)
    : IRequestHandler<ListEmployerAssignmentsQuery, IReadOnlyList<EmployerAssignmentResult>>
{
    public async Task<IReadOnlyList<EmployerAssignmentResult>> Handle(
        ListEmployerAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        var items = await assignments.ListByEmployerAsync(userContext.UserId, cancellationToken);
        var results = new List<EmployerAssignmentResult>(items.Count);
        foreach (var item in items.OrderByDescending(assignment => assignment.CreatedAt))
        {
            var session = await sessions.GetAsync(
                item.CandidateUserId,
                item.SessionId,
                cancellationToken);
            results.Add(
                new EmployerAssignmentResult(
                    item.SessionId,
                    item.TemplateId,
                    item.TemplateName,
                    item.JobTitle,
                    item.Company,
                    item.CandidateUserId,
                    session?.Status ?? InterviewSessionStatus.Started,
                    session?.OverviewStatus ?? OverviewStatus.NotAvailable,
                    item.CreatedAt));
        }

        return results;
    }
}
