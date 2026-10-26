using Microsoft.Extensions.Hosting;

namespace Tally.Infrastructure.Storage;

public sealed class LocalReceiptStore(IHostEnvironment env) : IReceiptStore
{
    private string Root => Path.Combine(env.ContentRootPath, "App_Data", "receipts");

    public async Task<string> SaveAsync(byte[] bytes, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        var ext = contentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
        var key = $"{Guid.CreateVersion7():N}{ext}";          // we choose the name, never the uploader
        await File.WriteAllBytesAsync(Path.Combine(Root, key), bytes, ct);
        return key;
    }

    // GetFileName strips any "../" so a key can never point outside the folder.
    public Stream OpenRead(string key) => File.OpenRead(Path.Combine(Root, Path.GetFileName(key)));
}