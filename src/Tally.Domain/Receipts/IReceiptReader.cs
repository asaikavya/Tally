namespace Tally.Domain.Receipts;

public interface IReceiptReader
{
    Task<ReceiptFields> ReadAsync(byte[] image, string contentType, CancellationToken ct);
}