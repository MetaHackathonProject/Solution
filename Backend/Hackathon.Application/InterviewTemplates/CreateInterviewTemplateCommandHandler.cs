using System.Security.Cryptography;
using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Domain.Accounts;
using Hackathon.Domain.InterviewTemplates;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class CreateInterviewTemplateCommandHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IInterviewTemplateStore templates)
    : IRequestHandler<CreateInterviewTemplateCommand, InterviewTemplateResult>
{
    public async Task<InterviewTemplateResult> Handle(
        CreateInterviewTemplateCommand request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        ValidateHeader(request.Name, request.JobTitle, request.Company, request.Language);
        request.Definition.Validate();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var template = new InterviewTemplate(
            userContext.UserId,
            Guid.NewGuid().ToString(),
            request.Name.Trim(),
            request.JobTitle.Trim(),
            request.Company.Trim(),
            request.Language.Trim(),
            NormalizeUrl(request.OfferUrl),
            await UniqueInviteCodeAsync(cancellationToken),
            request.Definition.ToJson(),
            now,
            now);

        await templates.SaveAsync(template, cancellationToken);
        return ToResult(template);
    }

    private async Task<string> UniqueInviteCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
            if (await templates.GetByInviteCodeAsync(code, cancellationToken) is null)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not allocate an invite code.");
    }

    internal static void ValidateHeader(string name, string jobTitle, string company, string language)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            throw new ArgumentException("Name must be at least 2 characters.");
        }

        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            throw new ArgumentException("Job title is required.");
        }

        if (string.IsNullOrWhiteSpace(company))
        {
            throw new ArgumentException("Company is required.");
        }

        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language is required.");
        }
    }

    internal static string? NormalizeUrl(string? offerUrl)
    {
        var value = offerUrl?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    internal static InterviewTemplateResult ToResult(InterviewTemplate template) =>
        new(
            template.TemplateId,
            template.Name,
            template.JobTitle,
            template.Company,
            template.Language,
            template.OfferUrl,
            template.InviteCode,
            InterviewTemplateDefinition.Parse(template.DefinitionJson),
            template.CreatedAt,
            template.UpdatedAt);
}
