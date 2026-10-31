using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Domain.Accounts;
using Hackathon.Domain.InterviewTemplates;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class UpdateInterviewTemplateCommandHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewTemplateStore templates)
    : IRequestHandler<UpdateInterviewTemplateCommand, InterviewTemplateResult?>
{
    public async Task<InterviewTemplateResult?> Handle(
        UpdateInterviewTemplateCommand request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        CreateInterviewTemplateCommandHandler.ValidateHeader(
            request.Name,
            request.JobTitle,
            request.Company,
            request.Language);
        request.Definition.Validate();

        var existing = await templates.GetAsync(
            userContext.UserId,
            request.TemplateId,
            cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var updated = existing with
        {
            Name = request.Name.Trim(),
            JobTitle = request.JobTitle.Trim(),
            Company = request.Company.Trim(),
            Language = request.Language.Trim(),
            OfferUrl = CreateInterviewTemplateCommandHandler.NormalizeUrl(request.OfferUrl),
            DefinitionJson = request.Definition.ToJson(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        await templates.SaveAsync(updated, cancellationToken);
        return CreateInterviewTemplateCommandHandler.ToResult(updated);
    }
}
