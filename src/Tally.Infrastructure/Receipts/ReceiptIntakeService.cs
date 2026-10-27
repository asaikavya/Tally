using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tally.Domain.Entities;
using Tally.Domain.Receipts;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Storage;

namespace Tally.Infrastructure.Receipts;

public sealed record ReceiptCorrection(decimal? Amount, DateOnly? PaidOn, string? SenderName, string? Memo, string? Reference);

public sealed class ReceiptIntakeService(
    TallyDbContext db, IReceiptStore store, IReceiptReader reader, ILogger<ReceiptIntakeService> log)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] Allowed = ["image/png", "image/jpeg", "image/webp"];
    // PNG starts 89 50 4E 47, JPEG starts FF D8 FF, WebP is "RIFF....WEBP".
    private static string? DetectImageType(byte[] b) =>
        b.Length >= 12 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 ? "image/png"
      : b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? "image/jpeg"
      : b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
                       && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P' ? "image/webp"
      : null;

    public async Task<Receipt> UploadAsync(Stream content, string contentType, string uploadedBy, Guid? payerId, CancellationToken ct)
    {

        if (!Allowed.Contains(contentType)) throw new ArgumentException("Please upload a PNG, JPG or WebP image.");

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        if (ms.Length == 0 || ms.Length > MaxBytes) throw new ArgumentException("The image must be under 5 MB.");
        var bytes = ms.ToArray();
        // Don't trust the declared type: check the file's real signature.
        var realType = DetectImageType(bytes)
            ?? throw new ArgumentException("That file isn't a PNG, JPG or WebP image.");
        contentType = realType;

        var receipt = new Receipt
        {
            UploadedAtUtc = DateTime.UtcNow,
            UploadedBy = uploadedBy,
            PayerId = payerId,
            ContentType = contentType,
            ImageHash = Convert.ToHexString(SHA256.HashData(bytes)),
            StorageKey = await store.SaveAsync(bytes, contentType, ct)
        };
        db.Receipts.Add(receipt);

        try
        {
            var f = await reader.ReadAsync(bytes, contentType, ct);
            receipt.AiRaw = JsonSerializer.Serialize(f);
            receipt.Amount = f.Amount;
            receipt.Currency = f.Currency;
            receipt.PaidOn = DateOnly.TryParseExact(f.PaidOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
            receipt.SenderName = f.SenderName;
            receipt.PaidTo = f.PaidTo;
            receipt.Memo = f.Memo;
            receipt.Reference = f.Reference;
            receipt.Method = f.Method;

            if (!f.LooksLikePaymentReceipt) { receipt.Status = ReceiptStatus.NeedsReview; receipt.Problem = "This doesn't look like a payment receipt."; }
            else if (f.Amount is null or <= 0) { receipt.Status = ReceiptStatus.NeedsReview; receipt.Problem = "We couldn't read the amount. Please type it."; }
            else receipt.Status = ReceiptStatus.Read;
        }
        catch (Exception ex)
        {
            // The AI failing must never lose the upload: a human can type the details.
            log.LogWarning(ex, "AI could not read receipt {ReceiptId}", receipt.Id);
            receipt.Status = ReceiptStatus.NeedsReview;
            receipt.Problem = "We couldn't read this image automatically. Please type the details.";
        }

        await MarkIfDuplicateAsync(receipt, ct);
        await db.SaveChangesAsync(ct);
        return receipt;
    }

    public async Task<Receipt?> CorrectAsync(Guid id, ReceiptCorrection c, CancellationToken ct)
    {
        var r = await db.Receipts.FindAsync([id], ct);
        if (r is null) return null;

        var edited = new HashSet<string>((r.EditedFields ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
        if (c.Amount != r.Amount) { r.Amount = c.Amount; edited.Add("Amount"); }
        if (c.PaidOn != r.PaidOn) { r.PaidOn = c.PaidOn; edited.Add("PaidOn"); }
        if (c.SenderName != r.SenderName) { r.SenderName = c.SenderName; edited.Add("SenderName"); }
        if (c.Memo != r.Memo) { r.Memo = c.Memo; edited.Add("Memo"); }
        if (c.Reference != r.Reference) { r.Reference = c.Reference; edited.Add("Reference"); }
        r.EditedFields = string.Join(",", edited);

        if (r.Status is ReceiptStatus.NeedsReview or ReceiptStatus.Duplicate && r.Amount > 0)
        {
            r.Status = ReceiptStatus.Read;
            r.Problem = null;
            r.DuplicateOfId = null;
        }
        await MarkIfDuplicateAsync(r, ct);
        await db.SaveChangesAsync(ct);
        return r;
    }


    // Same image, or same reference already on a receipt or a recorded payment → duplicate.
    private async Task MarkIfDuplicateAsync(Receipt r, CancellationToken ct)
    {
        var earlier = await db.Receipts.AsNoTracking()
            .Where(x => x.Id != r.Id && (x.ImageHash == r.ImageHash || (r.Reference != null && x.Reference == r.Reference)))
            .OrderBy(x => x.UploadedAtUtc)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        var paymentExists = r.Reference != null && await db.Payments.AnyAsync(p => p.ExternalId == r.Reference, ct);

        if (earlier is not null || paymentExists)
        {
            r.Status = ReceiptStatus.Duplicate;
            r.DuplicateOfId = earlier;
            r.Problem = "This receipt was already recorded.";
        }

    }
    // For receipts the AI couldn't read (e.g. a timeout): read the saved image again.
    public async Task<Receipt?> ReadAgainAsync(Guid id, CancellationToken ct)
    {
        var r = await db.Receipts.FindAsync([id], ct);
        if (r is null) return null;
        if (r.AiRaw is not null) return r;                     // already read: never overwrite the owner's corrections

        using var ms = new MemoryStream();
        await using (var file = store.OpenRead(r.StorageKey)) await file.CopyToAsync(ms, ct);

        try
        {
            var f = await reader.ReadAsync(ms.ToArray(), r.ContentType, ct);
            r.AiRaw = JsonSerializer.Serialize(f);
            r.Amount = f.Amount; r.Currency = f.Currency;
            r.PaidOn = DateOnly.TryParseExact(f.PaidOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
            r.SenderName = f.SenderName; r.PaidTo = f.PaidTo; r.Memo = f.Memo; r.Reference = f.Reference; r.Method = f.Method;
            (r.Status, r.Problem) = !f.LooksLikePaymentReceipt ? (ReceiptStatus.NeedsReview, "This doesn't look like a payment receipt.")
                                 : f.Amount is null or <= 0 ? (ReceiptStatus.NeedsReview, "We couldn't read the amount. Please type it.")
                                 : (ReceiptStatus.Read, (string?)null);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "AI still could not read receipt {ReceiptId}", r.Id);
            r.Problem = "Still couldn't read it. Please try again in a minute, or type the details.";
        }

        await MarkIfDuplicateAsync(r, ct);
        await db.SaveChangesAsync(ct);
        return r;
    }
}