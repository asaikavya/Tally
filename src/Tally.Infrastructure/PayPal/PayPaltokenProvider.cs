using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Tally.Infrastructure.PayPal;

// Singleton: one shared cache of the token for the whole app
public sealed class PayPalTokenProvider(IHttpClientFactory httpFactory, IOptions<PayPalOptions> options)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

        await _lock.WaitAsync(ct);                       // only one request refreshes the token
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

            var o = options.Value;
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.ClientId}:{o.ClientSecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var response = await httpFactory.CreateClient("PayPal").SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(PayPalJson.Options, ct)
                       ?? throw new InvalidOperationException("PayPal returned an empty token response.");

            _token = body.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(body.ExpiresIn - 60);   // refresh a minute early
            return _token;
        }
        finally { _lock.Release(); }
    }

    private sealed record TokenResponse(string AccessToken, int ExpiresIn);
}