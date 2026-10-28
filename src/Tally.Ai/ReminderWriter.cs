using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Tally.Domain.Messages;

namespace Tally.Ai;

// Drafts ONE reminder from the facts given. The owner always reviews it before PayPal sends it.
public sealed class ReminderWriter(IChatCompletionService chat, ILogger<ReminderWriter> log) : IReminderWriter
{
    private const string Instructions = """
        You write a short payment reminder that a small academy's owner will review and then send through PayPal.
        PayPal shows the invoice and a Pay Now button under your note, so do NOT include links, bank details or tables.
        Rules:
        - Write in the language code given: en = English, es = Spanish (neutral Latin American Spanish, "usted").
        - Warm and respectful. Never accusing. Never mention late fees, penalties or consequences.
        - Use ONLY the facts given. Never invent names, dates, amounts or classes.
        - Mention each class with its amount once, then the total.
        - If DaysLate > 0, say gently that the due date has passed. Otherwise say it is due soon.
        - Say that if they already paid another way, they can just reply and it will be updated.
        - Then one line inviting them to pay in one tap with the PayPal button below.
        - Sign off with the academy name.
        - Note: 60 to 110 words, plain text with short paragraphs. Subject: under 60 characters. No emojis.
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ReminderDraft> DraftAsync(ReminderFacts facts, CancellationToken ct)
    {
        var history = new ChatHistory(Instructions);
        history.AddUserMessage(JsonSerializer.Serialize(facts, Json));

        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(ReminderDraft),   // structured output: exactly { subject, note }
            Temperature = 0.4                         // a little warmth, still predictable
        };

        var reply = await chat.GetChatMessageContentAsync(history, settings, cancellationToken: ct);
        log.LogInformation("Reminder drafted by AI ({Chars} chars, {Language})", reply.Content?.Length ?? 0, facts.Language);

        var draft = JsonSerializer.Deserialize<ReminderDraft>(reply.Content ?? "{}", Json);
        if (draft is null || string.IsNullOrWhiteSpace(draft.Note) || string.IsNullOrWhiteSpace(draft.Subject))
            throw new InvalidOperationException("The AI returned an empty reminder.");
        return draft;
    }
}