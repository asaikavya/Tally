using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tally.Infrastructure.Webhooks;

namespace Tally.Infrastructure.Messaging;

public static class WebhookQueueSetup
{
    // No connection string → in-memory queue. Connection string → Azure Service Bus. Nothing else changes.
    public static IServiceCollection AddWebhookQueue(this IServiceCollection services, IConfiguration config, out string mode)
    {
        services.AddSingleton<WebhookEventRunner>();
        var connectionString = config["ServiceBus:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<InMemoryWebhookQueue>();
            services.AddSingleton<IWebhookQueue>(sp => sp.GetRequiredService<InMemoryWebhookQueue>());
            services.AddHostedService<WebhookProcessor>();
            mode = "in-memory";
        }
        else
        {
            var queue = config["ServiceBus:Queue"] ?? "paypal-webhooks";
            services.AddSingleton(new ServiceBusSettings(queue));
            services.AddSingleton(_ => new ServiceBusClient(connectionString));
            services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(queue));
            services.AddSingleton<IWebhookQueue, ServiceBusWebhookQueue>();
            services.AddHostedService<ServiceBusWebhookProcessor>();
            mode = $"Azure Service Bus ({queue})";
        }
        return services;
    }
}