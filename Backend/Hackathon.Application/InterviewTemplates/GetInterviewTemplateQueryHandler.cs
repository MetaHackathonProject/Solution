using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Domain.Accounts;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class GetInterviewTemplateQueryHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewTemplateStore templates)
    : IRequestHandler<GetInterviewTemplateQuery, InterviewTemplateResult?>
{
    public async Task<InterviewTemplateResult?> Handle(
        GetInterviewTemplateQuery request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        var template = await templates.GetAsync(
            userContext.UserId,
            request.TemplateId,
            cancellationToken);
        return template is null ? null : CreateInterviewTemplateCommandHandler.ToResult(template);
    }
}
