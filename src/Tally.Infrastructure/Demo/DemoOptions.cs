namespace Tally.Infrastructure.Demo;

public sealed class DemoOptions
{
    public const string Section = "Demo";

    public bool Enabled { get; set; }                     // Reset demo only works when this is true
    public string PayerEmail { get; set; } = "";          // sandbox personal account; all demo families use it
    public string? MeeraPaidInvoiceId { get; set; }       // sandbox invoices that were really paid once
    public string? LakshmiPaidInvoiceId { get; set; }
}