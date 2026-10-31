using Microsoft.EntityFrameworkCore;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;

namespace Tally.Infrastructure.Storage;

// Same contract as LocalReceiptStore, but the bytes live in SQL instead of on disk.
public sealed class DbReceiptStore(TallyDbContext db) : IReceiptStore
{
    public async Task<string> SaveAsync(byte[] bytes, string contentType, CancellationToken ct)
    {
        var ext = contentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
        var key = $"{Guid.CreateVersion7():N}{ext}";          // we choose the name, never the uploader
        db.ReceiptImages.Add(new ReceiptImage { Key = key, ContentType = contentType, Bytes = bytes });
        await db.SaveChangesAsync(ct);
        return key;
    }

    public Stream OpenRead(string key)
    {
        var bytes = db.ReceiptImages.AsNoTracking()
            .Where(i => i.Key == key)
            .Select(i => i.Bytes)
            .FirstOrDefault()
            ?? throw new FileNotFoundException("Receipt image not found.", key);   // same error as the file version
        return new MemoryStream(bytes, writable: false);
    }
}