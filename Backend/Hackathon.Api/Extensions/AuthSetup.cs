using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Hackathon.Api.Extensions;

internal static class AuthSetup
{
    public static IServiceCollection SetupAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var clientId = configuration["JWT:ClientId"];
        var authorityUrl = configuration["JWT:AuthorityUrl"];

        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityUrl);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.Audience = clientId;
            options.Authority = authorityUrl;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateAudience = false,
            };
        });

        services.AddAuthorization();

        return services;
    }
}
