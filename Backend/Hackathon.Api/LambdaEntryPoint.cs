namespace Hackathon.Api;

public class LambdaEntryPoint : Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var env = context.HostingEnvironment.EnvironmentName;

            config.AddSystemsManager(source =>
            {
                source.Path = $"/hackathon-solution/{env}";
                source.Optional = true;
            });
            config.AddEnvironmentVariables(prefix: "APP_");
        });

        builder.UseStartup<Startup>();
    }
}
