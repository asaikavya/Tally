using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tally.Infrastructure.Messaging;

namespace Tally.Infrastructure.Webhooks;

// Service Bus version: complete on success. On failure, leave the message so Service Bus retries it
// when its lock expires; after the queue's max delivery count (5) it moves to the dead-letter queue.
public sealed class ServiceBusWebhookProcessor(
    ServiceBusClient client, ServiceBusSettings settings, WebhookEventRunner runner, ILogger<ServiceBusWebhookProcessor> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor(settings.Queue, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,            // one at a time: simple, and gentle on PayPal's API
            AutoCompleteMessages = false,      // we decide when a message is done
        });

        processor.ProcessMessageAsync += async args =>
        {
            if (!Guid.TryParse(args.Message.Body.ToString(), out var id))
            {
                await args.DeadLetterMessageAsync(args.Message, "BadBody", "Not a webhook event id", args.CancellationToken);
                return;
            }

            try
            {
                await runner.RunAsync(id, args.CancellationToken);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not completed on purpose: Service Bus redelivers it later (a natural pause), then dead-letters it.
                log.LogWarning(ex, "Webhook event {Id} failed on try {Try}; Service Bus will retry", id, args.Message.DeliveryCount);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            log.LogError(args.Exception, "Service Bus error ({Source}) on {Entity}", args.ErrorSource, args.EntityPath);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { /* app is shutting down */ }
        await processor.StopProcessingAsync(CancellationToken.None);
    }
}