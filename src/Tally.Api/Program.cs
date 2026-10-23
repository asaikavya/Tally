using Microsoft.EntityFrameworkCore;
using Tally.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Register services (all BEFORE builder.Build())
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<TallyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TallyDb")));

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