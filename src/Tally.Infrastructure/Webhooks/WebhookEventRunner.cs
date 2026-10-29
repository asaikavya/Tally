using Microsoft.Extensions.DependencyInjection;

namespace Tally.Infrastructure.Webhooks;

// The one place that processes an event. Both queues use it, so they behave the same.
public sealed class WebhookEventRunner(IServiceScopeFactory scopes)
{
    public async Task RunAsync(Guid webhookEventId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();                     // fresh DbContext per event
        var handler = scope.ServiceProvider.GetRequiredService<PayPalEventHandler>();
        await handler.HandleAsync(webhookEventId, ct);              // throws on failure: the queue decides about retries
    }
}