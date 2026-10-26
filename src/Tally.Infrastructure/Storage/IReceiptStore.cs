namespace Tally.Infrastructure.Storage;

// Local disk now; Azure Blob Storage later. Same interface.
public interface IReceiptStore
{
    Task<string> SaveAsync(byte[] bytes, string contentType, CancellationToken ct);
    Stream OpenRead(string key);
}