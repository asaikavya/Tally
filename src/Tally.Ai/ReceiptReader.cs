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
        You read a screenshot of a payment receipt (Zelle, UPI, bank transfer, PayPal, cash slip).
        Extract ONLY what is visibly written. Never guess or infer. If a field is not visible, return null.
        - Amount: the money amount as a number, no currency symbol.
        - Currency: 3-letter code if you can tell (USD, INR...), else null.
        - PaidOn: the payment date as yyyy-MM-dd.
        - SenderName: the payer's name ONLY if shown (a payer's own receipt often doesn't show it).
        - PaidTo: the recipient's name as shown.
        - Memo: the note text; null if blank, "(none)" or missing.
        - Reference: confirmation number, UTR or transaction ID.
        - Method: Zelle, UPI, Bank transfer, PayPal, Cash or Other.
        - LooksLikePaymentReceipt: false if this is not a payment receipt at all.
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ReceiptFields> ReadAsync(byte[] image, string contentType, CancellationToken ct)
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