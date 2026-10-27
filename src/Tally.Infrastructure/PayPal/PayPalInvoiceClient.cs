using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Tally.Infrastructure.PayPal;

public sealed class PayPalInvoiceClient(IHttpClientFactory httpFactory, PayPalTokenProvider tokens)
{
    public async Task<InvoiceResponse> CreateDraftAsync(CreateInvoiceRequest body, CancellationToken ct)
    {
        using var request = await AuthorizedAsync(HttpMethod.Post, "v2/invoicing/invoices", ct);
        request.Headers.Add("Prefer", "return=representation");     // ask for the full invoice, including its id
        request.Content = JsonContent.Create(body, options: PayPalJson.Options);
        return await SendAsync<InvoiceResponse>(request, ct);
    }

    public async Task SendInvoiceAsync(string invoiceId, CancellationToken ct)
    {
        using var request = await AuthorizedAsync(HttpMethod.Post, $"v2/invoicing/invoices/{invoiceId}/send", ct);
        request.Content = JsonContent.Create(new SendInvoiceRequest(SendToRecipient: true, SendToInvoicer: false),
                                             options: PayPalJson.Options);
        using var response = await httpFactory.CreateClient("PayPal").SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<InvoiceResponse> GetAsync(string invoiceId, CancellationToken ct)
    {
        using var request = await AuthorizedAsync(HttpMethod.Get, $"v2/invoicing/invoices/{invoiceId}", ct);
        return await SendAsync<InvoiceResponse>(request, ct);
    }
    // "This much was paid outside PayPal." Returns PayPal's id for it (needed for Undo).
    public async Task<string> RecordPaymentAsync(string invoiceId, RecordPaymentRequest body, string requestId, CancellationToken ct)
    {
        using var request = await AuthorizedAsync(HttpMethod.Post, $"v2/invoicing/invoices/{invoiceId}/payments", ct);
        request.Headers.Add("PayPal-Request-Id", requestId);        // same id twice = PayPal won't record it twice
        request.Content = JsonContent.Create(body, options: PayPalJson.Options);
        var result = await SendAsync<RecordPaymentResponse>(request, ct);
        return result.PaymentId;
    }

    // Undo: remove that recorded payment from the invoice.
    public async Task DeletePaymentAsync(string invoiceId, string paymentId, CancellationToken ct)
    {
        using var request = await AuthorizedAsync(HttpMethod.Delete, $"v2/invoicing/invoices/{invoiceId}/payments/{paymentId}", ct);
        using var response = await httpFactory.CreateClient("PayPal").SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return;   // already gone: that's fine
        await EnsureSuccessAsync(response, ct);
    }

    private async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await httpFactory.CreateClient("PayPal").SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(PayPalJson.Options, ct)
               ?? throw new InvalidOperationException("PayPal returned an empty response.");
    }

    // PayPal's error body explains exactly what was wrong (and includes a debug_id), so surface it
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var details = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"PayPal returned {(int)response.StatusCode}: {details}");
    }
    // Asks PayPal: "did you really send this webhook?"
    public async Task<bool> VerifyWebhookAsync(WebhookHeaders h, string rawBody, string webhookId, CancellationToken ct)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
        var body = new VerifyWebhookRequest(h.AuthAlgo, h.CertUrl, h.TransmissionId, h.TransmissionSig,
                                            h.TransmissionTime, webhookId, doc.RootElement.Clone());

        using var request = await AuthorizedAsync(HttpMethod.Post, "v1/notifications/verify-webhook-signature", ct);
        request.Content = JsonContent.Create(body, options: PayPalJson.Options);
        var result = await SendAsync<VerifyWebhookResponse>(request, ct);
        return result.VerificationStatus == "SUCCESS";
    }
}