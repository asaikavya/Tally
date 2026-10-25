using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tally.Infrastructure.Messaging;

namespace Tally.Infrastructure.Webhooks;

// Runs for the lifetime of the app, processing queued events one at a time
public sealed class WebhookProcessor(IWebhookQueue queue, IServiceScopeFactory scopes, ILogger<WebhookProcessor> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();                       // fresh DbContext per event
                var handler = scope.ServiceProvider.GetRequiredService<PayPalEventHandler>();
                await handler.HandleAsync(id, stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to process webhook event {Id}", id); // one bad event never stops the loop
            }
        }
    }
}