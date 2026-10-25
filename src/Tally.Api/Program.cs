using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tally.Infrastructure.Billing;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Messaging;
using Tally.Infrastructure.PayPal;
using Tally.Infrastructure.Webhooks;


var builder = WebApplication.CreateBuilder(args);

// 1. Register services (all BEFORE builder.Build())
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<TallyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TallyDb")));

builder.Services.AddOptions<PayPalOptions>()
    .Bind(builder.Configuration.GetSection(PayPalOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();                    // missing keys fail at startup, not mid-request

builder.Services.AddHttpClient("PayPal", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IOptions<PayPalOptions>>().Value.BaseUrl;
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});

builder.Services.AddSingleton<PayPalTokenProvider>();
builder.Services.AddScoped<PayPalInvoiceClient>();
builder.Services.AddScoped<InvoicingService>();
builder.Services.AddSingleton<IWebhookQueue, InMemoryWebhookQueue>();
builder.Services.AddScoped<PayPalEventHandler>();
builder.Services.AddHostedService<WebhookProcessor>();

var app = builder.Build();

// 2. Use services (all AFTER builder.Build())
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TallyDbContext>();
    await DemoSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();