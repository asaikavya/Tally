using Microsoft.EntityFrameworkCore;
using Tally.Domain;
using Tally.Domain.Entities;

namespace Tally.Infrastructure.Data;

public class TallyDbContext(DbContextOptions<TallyDbContext> options) : DbContext(options)
{
    public DbSet<Organizer> Organizers => Set<Organizer>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Offering> Offerings => Set<Offering>();
    public DbSet<PricePlan> PricePlans => Set<PricePlan>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Payer> Payers => Set<Payer>();
    public DbSet<PayerAlias> PayerAliases => Set<PayerAlias>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);    // all money, one rule
        builder.Properties<string>().HaveMaxLength(200);       // no accidental nvarchar(max)
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        // One bill per enrollment per month: billing twice can't double-charge
        model.Entity<Charge>().HasIndex(c => new { c.EnrollmentId, c.PeriodStart }).IsUnique();
        model.Entity<Charge>().HasIndex(c => new { c.Status, c.PeriodStart });
        model.Entity<Charge>().Property(c => c.RowVersion).IsRowVersion();

        // One PayPal invoice per family per month
        model.Entity<Invoice>().HasIndex(i => new { i.PayerId, i.PeriodStart }).IsUnique();
        model.Entity<Invoice>().Property(i => i.PayUrl).HasMaxLength(500);

        // Each PayPal event is processed once
        model.Entity<WebhookEvent>().HasIndex(e => e.PayPalEventId).IsUnique();
        model.Entity<WebhookEvent>().Property(e => e.Payload).HasColumnType("nvarchar(max)");

        model.Entity<Payment>().HasIndex(p => p.ReceivedAtUtc);
        model.Entity<Payment>().HasIndex(p => p.ExternalId);
        model.Entity<Payment>().Property(p => p.Currency).HasMaxLength(3);
        model.Entity<Allocation>().Property(a => a.Reasons).HasMaxLength(1000);
        // Family pay links: unique, short enough to index
        model.Entity<Payer>().Property(p => p.PayToken).HasMaxLength(64);
        model.Entity<Payer>().HasIndex(p => p.PayToken).IsUnique();
        // Receipts: fast duplicate checks
        model.Entity<Receipt>().HasIndex(r => r.ImageHash);
        model.Entity<Receipt>().HasIndex(r => r.Reference);
        model.Entity<Receipt>().Property(r => r.ImageHash).HasMaxLength(64);
        model.Entity<Receipt>().Property(r => r.AiRaw).HasColumnType("nvarchar(max)");
        model.Entity<Receipt>().Property(r => r.Currency).HasMaxLength(3);


        // Never cascade-delete financial history
        foreach (var fk in model.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}