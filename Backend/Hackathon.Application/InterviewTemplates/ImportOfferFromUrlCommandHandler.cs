using Hackathon.Application.Accounts;
using Hackathon.Application.Context;
using Hackathon.Domain.Accounts;
using MediatR;

namespace Hackathon.Application.InterviewTemplates;

public sealed class ImportOfferFromUrlCommandHandler(
    IUserContext userContext,
    IUserProfileStore profiles,
    IOfferPageReader offerPages)
    : IRequestHandler<ImportOfferFromUrlCommand, ImportedOfferResult>
{
    public async Task<ImportedOfferResult> Handle(
        ImportOfferFromUrlCommand request,
        CancellationToken cancellationToken)
    {
        await UserRoleGuard.RequireAsync(
            profiles,
            userContext.UserId,
            UserRoles.Employer,
            cancellationToken);

        return await offerPages.ReadAsync(request.OfferUrl, cancellationToken);
    }
}
