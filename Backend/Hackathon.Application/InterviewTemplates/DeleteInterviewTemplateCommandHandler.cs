using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Domain.Accounts;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class DeleteInterviewTemplateCommandHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewTemplateStore templates)
    : IRequestHandler<DeleteInterviewTemplateCommand, bool>
{
    public async Task<bool> Handle(
        DeleteInterviewTemplateCommand request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        var existing = await templates.GetAsync(
            userContext.UserId,
            request.TemplateId,
            cancellationToken);
        if (existing is null)
        {
            return false;
        }

        await templates.DeleteAsync(userContext.UserId, request.TemplateId, cancellationToken);
        return true;
    }
}
