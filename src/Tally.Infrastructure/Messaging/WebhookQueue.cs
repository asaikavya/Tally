using System.Threading.Channels;

namespace Tally.Infrastructure.Messaging;

// The webhook endpoint only knows "put this event id on a queue". Which queue is decided in Program.cs.
public interface IWebhookQueue
{
    ValueTask EnqueueAsync(Guid webhookEventId, CancellationToken ct);
}

// Local dev / no Azure: fast and simple, but lost on restart (sync is the safety net).
public sealed class InMemoryWebhookQueue : IWebhookQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ValueTask EnqueueAsync(Guid id, CancellationToken ct) => _channel.Writer.WriteAsync(id, ct);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed record ServiceBusSettings(string Queue);