using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContentsRUs.Eventing.Listener
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddExternalEventListener(this IServiceCollection services, IConfiguration configuration)
        {
            // Register the listener as a hosted service
            services.AddHostedService(sp => new ExternalEventListenerService(
                sp.GetRequiredService<ILogger<ExternalEventListenerService>>(),
                configuration["RabbitMQ:HostName"] ?? "localhost",
                int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                configuration["RabbitMQ:UserName"] ?? "user",
                configuration["RabbitMQ:Password"] ?? "password",
                configuration["RabbitMQ:RoutingKey"] ?? "content.#"
            ));

            return services;
        }
    }
}