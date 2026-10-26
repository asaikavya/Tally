using Microsoft.EntityFrameworkCore;
using Tally.Domain;
using Tally.Domain.Entities;

namespace Tally.Infrastructure.Data;

// The demo story: November bills for 5 families, the Kumars' October history, Priya's yearly plan in September.
public static class DemoSeeder
{
    public static readonly DateOnly Sep = new(2026, 9, 1);
    public static readonly DateOnly Oct = new(2026, 10, 1);
    public static readonly DateOnly Nov = new(2026, 11, 1);

    public static async Task SeedIfEmptyAsync(TallyDbContext db, string payerEmail, CancellationToken ct = default)
    {
        if (await db.Organizers.AnyAsync(ct)) return;
        await SeedStoryAsync(db, payerEmail, ct);
    }

    // Returns payer ids by name, so the reset can bill and link them.
    public static async Task<Dictionary<string, Guid>> SeedStoryAsync(TallyDbContext db, string payerEmail, CancellationToken ct = default)
    {
        var org = new Organizer { Name = "Melody Music Academy", Type = OrganizerType.Teacher, TimeZone = "America/Los_Angeles", DueDayOfMonth = 10 };
        var fremont = new Location { OrganizerId = org.Id, Name = "Fremont" };
        var sanJose = new Location { OrganizerId = org.Id, Name = "San Jose" };
        var online = new Location { OrganizerId = org.Id, Name = "Online", IsOnline = true };

        Offering Offer(string name) => new() { OrganizerId = org.Id, Name = name };
        PricePlan Plan(Offering o, string name, BillingFrequency f, decimal amt) =>
            new() { OfferingId = o.Id, Name = name, Frequency = f, Amount = amt };

        var piano = Offer("Piano"); var guitar = Offer("Guitar"); var dance = Offer("Dance"); var drawing = Offer("Drawing");
        var pianoMonthly = Plan(piano, "Piano monthly", BillingFrequency.Monthly, 100m);
        var pianoYearly = Plan(piano, "Piano yearly", BillingFrequency.Yearly, 1100m);
        var guitarMonthly = Plan(guitar, "Guitar monthly", BillingFrequency.Monthly, 80m);
        var danceMonthly = Plan(dance, "Dance monthly", BillingFrequency.Monthly, 90m);
        var drawMonthly = Plan(drawing, "Drawing monthly", BillingFrequency.Monthly, 60m);

        var pianoTue = new Group { OfferingId = piano.Id, LocationId = fremont.Id, Name = "Piano Tue 5pm" };
        var guitarSat = new Group { OfferingId = guitar.Id, LocationId = sanJose.Id, Name = "Guitar Sat 10am" };
        var danceSun = new Group { OfferingId = dance.Id, LocationId = fremont.Id, Name = "Dance Sun 11am" };
        var drawOnl = new Group { OfferingId = drawing.Id, LocationId = online.Id, Name = "Drawing Online" };

        var payers = new List<Payer>(); var members = new List<Member>();
        var enrollments = new List<Enrollment>(); var charges = new List<Charge>();
        var payments = new List<Payment>(); var allocations = new List<Allocation>();

        DateOnly DueOf(DateOnly period) => new(period.Year, period.Month, org.DueDayOfMonth);

        Payer NewPayer(string name) { var p = new Payer { Name = name, Email = payerEmail }; payers.Add(p); return p; }
        Member NewMember(Payer p, string name, string country = "US")
        { var m = new Member { PayerId = p.Id, Name = name, Country = country }; members.Add(m); return m; }
        Enrollment Enroll(Member m, Group g, PricePlan plan)
        { var e = new Enrollment { MemberId = m.Id, GroupId = g.Id, PricePlanId = plan.Id, StartDate = Sep }; enrollments.Add(e); return e; }
        Charge Bill(Enrollment e, PricePlan plan, DateOnly period)
        { var c = new Charge { EnrollmentId = e.Id, PeriodStart = period, DueDate = DueOf(period), AmountDue = plan.Amount }; charges.Add(c); return c; }
        void Allocate(Payment pay, Charge charge, decimal amount, string reason)
        {
            allocations.Add(new Allocation { PaymentId = pay.Id, ChargeId = charge.Id, Amount = amount, MatchedBy = MatchedBy.Manual, Confidence = 1m, Reasons = reason });
            charge.ApplyPayment(amount);                     // the only way money is recorded, even in demo data
        }

        // Kumar family: 2 children, 3 classes → one PayPal bill. The star of the demo.
        var ravi = NewPayer("Ravi Kumar");
        var ananya = NewMember(ravi, "Ananya Kumar");
        var arjun = NewMember(ravi, "Arjun Kumar");
        var kPiano = Enroll(ananya, pianoTue, pianoMonthly);
        var kDance = Enroll(ananya, danceSun, danceMonthly);
        var kGuitar = Enroll(arjun, guitarSat, guitarMonthly);
        Bill(kPiano, pianoMonthly, Nov); Bill(kDance, danceMonthly, Nov); Bill(kGuitar, guitarMonthly, Nov);

        // Kumar October history: $170 Zelle from "R KUMAR" for dance + guitar, piano in cash.
        // This is the "paid the same way last month" clue for the Payment Detective.
        var octPiano = Bill(kPiano, pianoMonthly, Oct);
        var octDance = Bill(kDance, danceMonthly, Oct);
        var octGuitar = Bill(kGuitar, guitarMonthly, Oct);
        var octZelle = new Payment
        {
            Source = PaymentSource.Zelle,
            Amount = 170m,
            Currency = "USD",
            ReceivedAtUtc = new DateTime(2026, 10, 9, 19, 30, 0, DateTimeKind.Utc),
            SenderText = "R KUMAR",
            ExternalId = "DEMO-OCT-ZELLE"
        };
        var octCash = new Payment
        {
            Source = PaymentSource.Cash,
            Amount = 100m,
            Currency = "USD",
            ReceivedAtUtc = new DateTime(2026, 10, 8, 17, 0, 0, DateTimeKind.Utc),
            SenderText = "Ravi Kumar at the front desk"
        };
        payments.Add(octZelle); payments.Add(octCash);
        Allocate(octZelle, octDance, 90m, "Owner approved: $170 Zelle from R KUMAR (dance + guitar)");
        Allocate(octZelle, octGuitar, 80m, "Owner approved: $170 Zelle from R KUMAR (dance + guitar)");
        Allocate(octCash, octPiano, 100m, "Recorded at the front desk");

        // Families who pay through PayPal (their paid sandbox invoices get linked by the reset).
        var meera = NewPayer("Meera Patel");
        Bill(Enroll(NewMember(meera, "Rohit Patel"), pianoTue, pianoMonthly), pianoMonthly, Nov);
        var lakshmi = NewPayer("Lakshmi Iyer");
        Bill(Enroll(NewMember(lakshmi, "Rohan Iyer"), guitarSat, guitarMonthly), guitarMonthly, Nov);   // the second "Rohan"

        // Families paying from abroad, not paid yet.
        var lucia = NewPayer("Lucia Lopez");
        Bill(Enroll(NewMember(lucia, "Diego Lopez", "MX"), danceSun, danceMonthly), danceMonthly, Nov);
        var rohan = NewPayer("Rohan Sharma");
        Bill(Enroll(NewMember(rohan, "Rohan Sharma", "IN"), drawOnl, drawMonthly), drawMonthly, Nov);

        // Yearly plan: billed and paid in September, so it never touches November's numbers.
        var priya = NewPayer("Priya Nair");
        var kavyaYear = Bill(Enroll(NewMember(priya, "Kavya Nair"), pianoTue, pianoYearly), pianoYearly, Sep);
        var sepCheck = new Payment
        {
            Source = PaymentSource.Other,
            Amount = 1100m,
            Currency = "USD",
            ReceivedAtUtc = new DateTime(2026, 9, 8, 17, 0, 0, DateTimeKind.Utc),
            SenderText = "Check #1042 from Priya Nair"
        };
        payments.Add(sepCheck);
        Allocate(sepCheck, kavyaYear, 1100m, "Yearly plan paid by check");

        // A name Ravi has paid from before, learned from October.
        db.Add(new PayerAlias { PayerId = ravi.Id, AliasText = "R KUMAR", Source = "Zelle" });

        db.Add(org);
        db.AddRange(fremont, sanJose, online, piano, guitar, dance, drawing);
        db.AddRange(pianoMonthly, pianoYearly, guitarMonthly, danceMonthly, drawMonthly);
        db.AddRange(pianoTue, guitarSat, danceSun, drawOnl);
        db.AddRange(payers); db.AddRange(members); db.AddRange(enrollments); db.AddRange(charges);
        db.AddRange(payments); db.AddRange(allocations);
        await db.SaveChangesAsync(ct);

        return payers.ToDictionary(p => p.Name, p => p.Id);
    }
}