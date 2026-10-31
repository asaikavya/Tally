using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tally.Ai;
using Tally.Infrastructure.Billing;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Demo;
using Tally.Infrastructure.Messages;
using Tally.Infrastructure.Messaging;
using Tally.Infrastructure.Payments;
using Tally.Infrastructure.PayPal;
using Tally.Infrastructure.Receipts;
using Tally.Infrastructure.Storage;
using Tally.Infrastructure.Webhooks;


var builder = WebApplication.CreateBuilder(args);

// 1. Register services (all BEFORE builder.Build())
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
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
builder.Services.AddWebhookQueue(builder.Configuration, out var webhookQueueMode);
builder.Services.AddScoped<PayPalEventHandler>();
builder.Services.AddOptions<DemoOptions>().Bind(builder.Configuration.GetSection(DemoOptions.Section));
builder.Services.AddScoped<DemoResetService>();
builder.Services.AddTallyAi(builder.Configuration);
builder.Services.AddScoped<IReceiptStore, DbReceiptStore>();
builder.Services.AddScoped<ReceiptIntakeService>();
builder.Services.AddScoped<DetectiveService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<PayPalNoteService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<ManualPaymentService>();
builder.Services.AddScoped<ReminderService>();

builder.Services.AddDbContext<TallyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TallyDb"),
        sql => sql.EnableRetryOnFailure()));

var app = builder.Build();
app.Logger.LogInformation("Webhook queue: {Mode}", webhookQueueMode);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();               // in the cloud, Render handles HTTPS in front of the app
}

// Seed the sample academy wherever demo mode is on (local AND live site), only if the database is empty.
var demoSettings = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
if (demoSettings.Enabled)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TallyDbContext>();
    await DemoSeeder.SeedIfEmptyAsync(db, demoSettings.PayerEmail);
}

app.UseDefaultFiles();                       // serves the React app's index.html
app.UseStaticFiles();                        // ...and its JS/CSS from wwwroot
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("ok"));   // for uptime checks: never touches the database
app.MapControllers();
app.MapFallbackToFile("index.html");         // React routes like /pay/... and /receipts/... load the app

app.Run();