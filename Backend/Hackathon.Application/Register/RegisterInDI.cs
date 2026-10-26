using Hackathon.Application.InterviewPreparation;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon.Application.Register;

public static class RegisterInDI
{
    public static void RegisterApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(StartInterviewPreparationCommand).Assembly);
        });
    }
}
