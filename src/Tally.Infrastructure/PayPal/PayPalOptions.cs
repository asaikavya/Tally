using System.ComponentModel.DataAnnotations;

namespace Tally.Infrastructure.PayPal;

public sealed class PayPalOptions
{
    public const string Section = "PayPal";

    [Required] public string BaseUrl { get; set; } = "";
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
    public string WebhookId { get; set; } = "";
    public bool SkipWebhookVerification { get; set; }   // Development only, for PayPal's simulator
}