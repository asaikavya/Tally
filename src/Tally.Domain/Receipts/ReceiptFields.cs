namespace Tally.Domain.Receipts;

// What the AI is allowed to tell us: only what's visible on the image. Null = not visible.
public sealed record ReceiptFields(
    bool LooksLikePaymentReceipt,
    decimal? Amount,
    string? Currency,
    string? PaidOn,        // "yyyy-MM-dd"; parsed in C#, never trusted blindly
    string? SenderName,
    string? PaidTo,
    string? Memo,
    string? Reference,     // confirmation / UTR / transaction ID
    string? Method);       // "Zelle", "UPI", "Bank transfer", ...