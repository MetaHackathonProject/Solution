using Hackathon.Api.Extensions;
using Hackathon.Application.Register;
using Hackathon.Infrastructure.Register;

namespace Hackathon.Api;

public sealed class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddSwaggerGen();

        services.RegisterApplication();
        services.RegisterInfrastructure(configuration);
        services.SetupUserApiContext();
        services.SetupAuth(configuration);
        services.SetupCors(configuration);
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseCors();
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
