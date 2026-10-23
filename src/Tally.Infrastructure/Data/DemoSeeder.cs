using Microsoft.EntityFrameworkCore;
using Tally.Domain;
using Tally.Domain.Entities;

namespace Tally.Infrastructure.Data;

public static class DemoSeeder
{
    public static async Task SeedAsync(TallyDbContext db)
    {
        if (await db.Organizers.AnyAsync()) return;   // already seeded

        var org = new Organizer
        {
            Name = "Melody Music Academy",
            Type = OrganizerType.Teacher,
            TimeZone = "America/Los_Angeles",
            DueDayOfMonth = 10
        };
        var fremont = new Location { OrganizerId = org.Id, Name = "Fremont" };
        var sanJose = new Location { OrganizerId = org.Id, Name = "San Jose" };
        var online = new Location { OrganizerId = org.Id, Name = "Online", IsOnline = true };

        // Offerings and their price plans
        Offering Offer(string name) => new() { OrganizerId = org.Id, Name = name };
        PricePlan Plan(Offering o, string name, BillingFrequency f, decimal amt) =>
            new() { OfferingId = o.Id, Name = name, Frequency = f, Amount = amt };

        var piano = Offer("Piano"); var guitar = Offer("Guitar");
        var dance = Offer("Dance"); var drawing = Offer("Drawing");

        var pianoMonthly = Plan(piano, "Piano monthly", BillingFrequency.Monthly, 100m);
        var pianoYearly = Plan(piano, "Piano yearly", BillingFrequency.Yearly, 1100m);
        var guitarMonthly = Plan(guitar, "Guitar monthly", BillingFrequency.Monthly, 80m);
        var danceMonthly = Plan(dance, "Dance monthly", BillingFrequency.Monthly, 90m);
        var drawMonthly = Plan(drawing, "Drawing monthly", BillingFrequency.Monthly, 60m);

        // Classes
        var pianoTue = new Group { OfferingId = piano.Id, LocationId = fremont.Id, Name = "Piano Tue 5pm" };
        var guitarSat = new Group { OfferingId = guitar.Id, LocationId = sanJose.Id, Name = "Guitar Sat 10am" };
        var danceSun = new Group { OfferingId = dance.Id, LocationId = fremont.Id, Name = "Dance Sun 11am" };
        var drawOnl = new Group { OfferingId = drawing.Id, LocationId = online.Id, Name = "Drawing Online" };

        // Payers and members
        var payers = new List<Payer>(); var members = new List<Member>();
        var enrollments = new List<Enrollment>(); var charges = new List<Charge>();
        var nov = new DateOnly(2026, 11, 1);
        var due = new DateOnly(2026, 11, org.DueDayOfMonth);

        Payer NewPayer(string name) { var p = new Payer { Name = name }; payers.Add(p); return p; }
        Member NewMember(Payer p, string name, string country = "US")
        { var m = new Member { PayerId = p.Id, Name = name, Country = country }; members.Add(m); return m; }
        void Enroll(Member m, Group g, PricePlan plan)
        {
            var e = new Enrollment { MemberId = m.Id, GroupId = g.Id, PricePlanId = plan.Id, StartDate = nov };
            enrollments.Add(e);
            charges.Add(new Charge { EnrollmentId = e.Id, PeriodStart = nov, DueDate = due, AmountDue = plan.Amount });
        }

        var ravi = NewPayer("Ravi Kumar");                    // one family, three charges, one bill
        var ananya = NewMember(ravi, "Ananya Kumar");
        Enroll(ananya, pianoTue, pianoMonthly);
        Enroll(ananya, danceSun, danceMonthly);
        Enroll(NewMember(ravi, "Arjun Kumar"), guitarSat, guitarMonthly);

        Enroll(NewMember(NewPayer("Meera Patel"), "Rohit Patel"), pianoTue, pianoMonthly);
        Enroll(NewMember(NewPayer("Priya Nair"), "Kavya Nair"), pianoTue, pianoYearly);   // yearly plan
        Enroll(NewMember(NewPayer("Sai"), "Sai"), guitarSat, guitarMonthly);
        Enroll(NewMember(NewPayer("Rohan Sharma"), "Rohan Sharma", "IN"), drawOnl, drawMonthly); // international
        Enroll(NewMember(NewPayer("Lakshmi Iyer"), "Rohan Iyer"), guitarSat, guitarMonthly);  // second "Rohan"
        Enroll(NewMember(NewPayer("Lucia Lopez"), "Diego Lopez", "MX"), drawOnl, drawMonthly);
        // add ~6 more the same way to reach about 15 members

        db.Add(new PayerAlias { PayerId = ravi.Id, AliasText = "R Kumar", Source = "Zelle" });

        db.Add(org);
        db.AddRange(fremont, sanJose, online, piano, guitar, dance, drawing);
        db.AddRange(pianoMonthly, pianoYearly, guitarMonthly, danceMonthly, drawMonthly);
        db.AddRange(pianoTue, guitarSat, danceSun, drawOnl);
        db.AddRange(payers); db.AddRange(members); db.AddRange(enrollments); db.AddRange(charges);
        await db.SaveChangesAsync();
    }
}