using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Tally.Domain.Receipts;

namespace Tally.Ai;

// Reads ONE receipt image. It never sees the families list: matching is Tally's own code (Step 9).
public sealed class ReceiptReader(IChatCompletionService chat, ILogger<ReceiptReader> log) : IReceiptReader
{
    private const string Instructions = """
        You read a screenshot or photo of a payment receipt: Zelle, UPI, bank or international transfer,
        PayPal, a check or mobile check deposit, or a cash slip.
        Extract ONLY what is visibly written. Never guess or infer. If a field is not visible, return null.
        - Amount: the money amount as a number, no currency symbol.
          If two amounts are shown (sent vs received, e.g. an international transfer), use the amount RECEIVED.
        - Currency: 3-letter code of that same amount if you can tell (USD, INR...), else null.
        - PaidOn: the payment, transfer or deposit date as yyyy-MM-dd.
        - SenderName: the payer's name ONLY if shown ("From", the check writer). A payer's own receipt often doesn't show it.
        - PaidTo: the recipient's name as shown ("To", "Pay to").
        - Memo: the note or memo text; null if blank, "(none)" or missing.
        - Reference: confirmation number, UTR, transaction ID or deposit ID. For a check with none of these, the check number.
        - Method: exactly one of Zelle, UPI, Bank transfer, Check, PayPal, Cash, Other.
          International or wire transfers count as Bank transfer. A check deposit counts as Check.
        - LooksLikePaymentReceipt: true for any of the receipt types above, including check deposits.
          false only if the image is not a payment at all (a photo, a chat, a bill that hasn't been paid).
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // Temporary failures (timeouts, overload, rate limit) usually pass in seconds: try up to 3 times.
    private static readonly int[] Transient = [408, 429, 500, 502, 503, 504];

    public async Task<ReceiptFields> ReadAsync(byte[] image, string contentType, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ReadOnceAsync(image, contentType, ct);
            }
            catch (HttpOperationException ex) when (attempt < 3 && ex.StatusCode is { } code && Transient.Contains((int)code))
            {
                var wait = TimeSpan.FromSeconds(attempt * 2);     // 2s, then 4s
                log.LogWarning("AI call failed with {Status}; retrying in {Seconds}s (attempt {Attempt})",
                    (int)code, wait.TotalSeconds, attempt);
                await Task.Delay(wait, ct);
            }
        }
    }

    public async Task<ReceiptFields> ReadOnceAsync(byte[] image, string contentType, CancellationToken ct)
    {
        var history = new ChatHistory(Instructions);
        history.AddUserMessage(new ChatMessageContentItemCollection
        {
            new TextContent("Read this receipt."),
            new ImageContent(image, contentType)
        });

        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(ReceiptFields),   // structured output: the model must answer in this exact shape
            Temperature = 0                           // reading, not creativity
        };

        var reply = await chat.GetChatMessageContentAsync(history, settings, cancellationToken: ct);
        log.LogInformation("Receipt read by AI ({Chars} chars)", reply.Content?.Length ?? 0);

        return JsonSerializer.Deserialize<ReceiptFields>(reply.Content ?? "{}", Json)
               ?? throw new InvalidOperationException("The AI returned an empty answer.");
    }
}