using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Domain.Accounts;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class ListInterviewTemplatesQueryHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewTemplateStore templates)
    : IRequestHandler<ListInterviewTemplatesQuery, IReadOnlyList<InterviewTemplateResult>>
{
    public async Task<IReadOnlyList<InterviewTemplateResult>> Handle(
        ListInterviewTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        var items = await templates.ListByEmployerAsync(userContext.UserId, cancellationToken);
        return items.Select(CreateInterviewTemplateCommandHandler.ToResult).ToList();
    }
}
