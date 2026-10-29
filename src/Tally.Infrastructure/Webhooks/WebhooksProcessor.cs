using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tally.Infrastructure.Messaging;

namespace Tally.Infrastructure.Webhooks;

// In-memory version: one event at a time; a failure is logged and the loop carries on.
public sealed class WebhookProcessor(InMemoryWebhookQueue queue, WebhookEventRunner runner, ILogger<WebhookProcessor> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await runner.RunAsync(id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Failed to process webhook event {Id}", id);   // one bad event never stops the loop
            }
        }
    }
}