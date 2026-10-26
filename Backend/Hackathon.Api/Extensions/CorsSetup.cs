namespace Hackathon.Api.Extensions;

internal static class CorsSetup
{
    public static IServiceCollection SetupCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration["Cors:AllowedOrigins"];
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedOrigins);

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
