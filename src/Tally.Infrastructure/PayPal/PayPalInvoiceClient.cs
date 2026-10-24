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
}