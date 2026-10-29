using Azure.Messaging.ServiceBus;

namespace Tally.Infrastructure.Messaging;

// Live site: the event id goes to Azure Service Bus, which keeps it until it's processed.
public sealed class ServiceBusWebhookQueue(ServiceBusSender sender) : IWebhookQueue
{
    public async ValueTask EnqueueAsync(Guid webhookEventId, CancellationToken ct)
    {
        var message = new ServiceBusMessage(webhookEventId.ToString())
        {
            MessageId = webhookEventId.ToString(),     // same event, same id
            ContentType = "text/plain",
            Subject = "paypal-webhook",
        };
        await sender.SendMessageAsync(message, ct);
    }
}