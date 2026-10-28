using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Tally.Domain.Entities;
using Tally.Domain.Messages;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.PayPal;
using static Tally.Domain.Entities.PayPalNote;

namespace Tally.Infrastructure.Messages;

public sealed record DraftRequest(Guid PayerId, string? Language);
public sealed record DraftResult(string Subject, string Note, string Language, bool AiWritten,
                                 bool CanSend, string? WhyNot, DateTime? LastSentAtUtc);
public sealed record SendReminderRequest(Guid PayerId, string Subject, string Note, string Language);
public sealed record SendReminderResult(DateTime SentAtUtc, List<string> PayPalInvoiceIds);

// AI drafts, the owner edits, PayPal sends. Plain C# decides WHETHER a reminder may go out.
public sealed class ReminderService(
    TallyDbContext db, IReminderWriter writer, PayPalInvoiceClient paypal,
    IConfiguration config, ILogger<ReminderService> log)
{
    private static readonly string[] Languages = ["en", "es"];
    private static readonly string[] Remindable = ["SENT", "UNPAID", "PARTIALLY_PAID"];
    private string Academy => config["Academy:Name"] ?? "the academy";

    private sealed record Open(Guid? InvoiceId, string Student, string Offering, decimal Remaining, DateOnly DueDate, bool SaysPaid);

    private Task<List<Open>> OpenChargesAsync(Guid payerId, CancellationToken ct) =>
        db.Charges.AsNoTracking()
            .Where(c => c.Enrollment!.Member!.PayerId == payerId && c.AmountDue > c.AmountPaid)
            .OrderBy(c => c.DueDate).ThenBy(c => c.Enrollment!.Member!.Name).ThenBy(c => c.Enrollment!.PricePlan!.Offering!.Name)
            .Select(c => new Open(c.InvoiceId, c.Enrollment!.Member!.Name, c.Enrollment!.PricePlan!.Offering!.Name,
                                  c.AmountDue - c.AmountPaid, c.DueDate, c.ParentSaysPaidAtUtc != null))
            .ToListAsync(ct);

    // The rules, in one place. null = a reminder may go out.
    private async Task<(string? WhyNot, List<Invoice> Invoices, DateTime? LastSent)> CheckAsync(
        Guid payerId, List<Open> open, CancellationToken ct)
    {
        var last = await db.ReminderLogs.AsNoTracking().Where(l => l.PayerId == payerId)
                                        .MaxAsync(l => (DateTime?)l.SentAtUtc, ct);
        var invoiceIds = open.Where(o => o.InvoiceId != null).Select(o => o.InvoiceId!.Value).Distinct().ToList();
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id) && Remindable.Contains(i.PayPalStatus))
            .ToListAsync(ct);

        string? why =
            open.Count == 0 ? "Nothing is owed, so there's nothing to remind about."
          : open.Any(o => o.SaysPaid) ? "They said they already paid. Confirm it, or check your bank first."
          : invoices.Count == 0 ? "There's no open PayPal bill for these classes."
          : last is { } t && DateTime.UtcNow - t < TimeSpan.FromHours(24) ? "Already reminded in the last 24 hours."
          : null;
        return (why, invoices, last);
    }

    public async Task<DraftResult?> DraftAsync(DraftRequest req, DateOnly today, CancellationToken ct)
    {
        var payer = await db.Payers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.PayerId, ct);
        if (payer is null) return null;

        var lang = req.Language is { } l && Languages.Contains(l) ? l
                 : Languages.Contains(payer.PreferredLanguage) ? payer.PreferredLanguage : "en";
        var open = await OpenChargesAsync(payer.Id, ct);
        var (why, _, last) = await CheckAsync(payer.Id, open, ct);

        var facts = new ReminderFacts(
            First(payer.Name), Academy,
            open.Select(o => new ReminderLine(First(o.Student), o.Offering, o.Remaining)).ToList(),
            open.Sum(o => o.Remaining),
            open.Count == 0 ? 0 : Math.Max(0, today.DayNumber - open.Min(o => o.DueDate).DayNumber),
            lang);

        // Can't send anyway? Don't spend an AI call: show the plain template as a preview.
        if (why is not null)
        {
            var preview = Template(facts);
            return new DraftResult(preview.Subject, preview.Note, lang, false, false, why, last);
        }

        ReminderDraft draft;
        var aiWritten = true;
        try
        {
            draft = await writer.DraftAsync(facts, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "AI couldn't draft a reminder; using the plain template");
            draft = Template(facts);
            aiWritten = false;
        }

        draft = WithPayPalLine(draft, lang);
        return new DraftResult(Trim(draft.Subject, 120), Trim(draft.Note, 1500), lang, aiWritten, true, null, last);
    }

    public async Task<(SendReminderResult? Result, string? Problem)> SendAsync(SendReminderRequest req, CancellationToken ct)
    {
        var subject = req.Subject?.Trim() ?? "";
        var note = req.Note?.Trim() ?? "";
        if (subject.Length is 0 or > 120) return (null, "The subject must be 1 to 120 characters.");
        if (note.Length is < 20 or > 1500) return (null, "The message must be 20 to 1,500 characters.");
        var lang = Languages.Contains(req.Language) ? req.Language : "en";

        var open = await OpenChargesAsync(req.PayerId, ct);
        var (why, invoices, _) = await CheckAsync(req.PayerId, open, ct);       // re-check: never trust the screen
        if (why is not null) return (null, why);

        var sent = new List<string>();
        string? problem = null;
        foreach (var inv in invoices)
        {
            try
            {
                await paypal.RemindAsync(inv.PayPalInvoiceId, new RemindRequest(subject, note, SendToInvoicer: false, SendToRecipient: true), ct);
                db.ReminderLogs.Add(new ReminderLog
                {
                    PayerId = req.PayerId,
                    InvoiceId = inv.Id,
                    Language = lang,
                    Subject = subject,
                    Note = note,
                    SentAtUtc = DateTime.UtcNow,
                });
                sent.Add(inv.PayPalInvoiceId);
            }
            catch (HttpRequestException ex)
            {
                log.LogWarning(ex, "PayPal didn't accept the reminder for invoice {Invoice}", inv.PayPalInvoiceId);
                problem = "PayPal didn't accept the reminder. Try again in a moment.";
                break;
            }
        }

        await db.SaveChangesAsync(ct);          // keep a log of whatever did go out
        return sent.Count == 0 ? (null, problem) : (new SendReminderResult(DateTime.UtcNow, sent), null);
    }

    // ---- helpers ----

    private static string First(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;

    private static string Usd(decimal n) => "$" + n.ToString(n % 1 == 0 ? "#,0" : "#,0.00", CultureInfo.InvariantCulture);

    private static string Trim(string s, int max) => s.Length <= max ? s.Trim() : s[..max].TrimEnd();

    // Every reminder ends by pointing to PayPal, even if the AI forgot.
    private static ReminderDraft WithPayPalLine(ReminderDraft d, string lang) =>
        d.Note.Contains("PayPal", StringComparison.OrdinalIgnoreCase)
            ? d
            : d with
            {
                Note = d.Note.TrimEnd() + "\n\n" + (lang == "es"
                ? "Puede pagar con un solo toque usando el botón de PayPal de abajo."
                : "You can pay in one tap with the PayPal button below.")
            };

    // Used when the AI is unavailable, or as a preview when a reminder can't be sent.
    private static ReminderDraft Template(ReminderFacts f)
    {
        if (f.Language == "es")
        {
            var clases = string.Join(", ", f.Lines.Select(l => $"{l.Offering} de {l.Student} ({Usd(l.Amount)})"));
            return new($"Recordatorio de pago · {f.AcademyName}",
                $"Hola {f.ParentFirstName}:\n\nLe recordamos amablemente el pago de {clases}. El total es {Usd(f.Total)}."
                + (f.DaysLate > 0 ? " La fecha de vencimiento ya pasó." : "")
                + "\n\nSi ya pagó por otro medio, responda a este mensaje y lo actualizamos."
                + "\n\nPuede pagar con un solo toque usando el botón de PayPal de abajo."
                + $"\n\nGracias,\n{f.AcademyName}");
        }

        var classes = string.Join(", ", f.Lines.Select(l => $"{l.Student}'s {l.Offering} ({Usd(l.Amount)})"));
        return new($"Payment reminder · {f.AcademyName}",
            $"Hi {f.ParentFirstName},\n\nA friendly reminder about {classes}. The total is {Usd(f.Total)}."
            + (f.DaysLate > 0 ? " The due date has passed." : "")
            + "\n\nIf you've already paid another way, just reply and we'll update it."
            + "\n\nYou can pay in one tap with the PayPal button below."
            + $"\n\nThank you,\n{f.AcademyName}");
    }
}