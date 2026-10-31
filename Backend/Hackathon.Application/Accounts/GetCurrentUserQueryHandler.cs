using Hackathon.Application.Context;
using MediatR;

namespace Hackathon.Application.Accounts;

public sealed class GetCurrentUserQueryHandler(
    IUserContext userContext,
    IUserProfileStore profiles) : IRequestHandler<GetCurrentUserQuery, CurrentUserResult>
{
    public async Task<CurrentUserResult> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userContext.UserId, cancellationToken);
        return new CurrentUserResult(userContext.UserId, userContext.Email, profile?.Role);
    }
}
