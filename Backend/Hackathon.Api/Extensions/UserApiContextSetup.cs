using Hackathon.Api.Context;
using Hackathon.Application.Context;

namespace Hackathon.Api.Extensions;

public static class UserApiContextSetup
{
    public static IServiceCollection SetupUserApiContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserApiContext>();
        return services;
    }
}
