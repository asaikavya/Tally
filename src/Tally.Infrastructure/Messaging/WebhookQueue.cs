using System.Threading.Channels;

namespace Tally.Infrastructure.Messaging;

public interface IWebhookQueue
{
    ValueTask EnqueueAsync(Guid webhookEventId, CancellationToken ct);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
}

// In-memory for now; swapped for Azure Service Bus on Nov 2 without touching the endpoint or processor
public sealed class InMemoryWebhookQueue : IWebhookQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ValueTask EnqueueAsync(Guid id, CancellationToken ct) => _channel.Writer.WriteAsync(id, ct);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}