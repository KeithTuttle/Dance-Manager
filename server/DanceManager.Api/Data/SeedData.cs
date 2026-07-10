using DanceManager.Api.Auth;
using DanceManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Data;

/// <summary>
/// Idempotent development seeder. Runs on startup and populates a small,
/// realistic dataset so every page has data to render. Entirely defensive:
/// if Postgres is unavailable (or anything else goes wrong) it is a silent
/// no-op and must never crash the application.
/// </summary>
public class DevSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DevSeeder> _logger;

    public DevSeeder(IServiceProvider services, ILogger<DevSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // Postgres may be absent — never crash if we cannot reach it.
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                _logger.LogInformation("DevSeeder: database not reachable; skipping seed.");
                return;
            }

            // Bypass the tenant query filter for the existence check (no tenant is
            // resolved at startup, so a filtered query would always look empty).
            if (await db.Studios.IgnoreQueryFilters().AnyAsync(cancellationToken))
            {
                _logger.LogInformation("DevSeeder: studios already present; skipping seed.");
                return;
            }

            await SeedAsync(db, currentTenant, config, cancellationToken);
            _logger.LogInformation("DevSeeder: seed data applied.");
        }
        catch (Exception ex)
        {
            // Missing/unavailable DB (or any transient failure) is a no-op.
            _logger.LogWarning(ex, "DevSeeder: seeding skipped due to an error.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedAsync(
        AppDbContext db, ICurrentTenant currentTenant, IConfiguration config, CancellationToken ct)
    {
        // Demo tenant that owns all seeded data. Tenants are not tenant-scoped, so
        // this insert isn't stamped/filtered.
        var tenant = new Tenant { Name = "Demo Studio Group" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        // Optionally attach a real Clerk user to the demo tenant so signing in with
        // that account locally lands on the seeded data (set Clerk:DevUserId).
        var devUserId = config["Clerk:DevUserId"];
        if (!string.IsNullOrWhiteSpace(devUserId))
        {
            db.Memberships.Add(new Membership
            {
                TenantId = tenant.Id,
                ClerkUserId = devUserId,
                Role = MembershipRole.Owner,
            });
            await db.SaveChangesAsync(ct);
        }

        // From here on, every seeded ITenantScoped row is auto-stamped with this tenant.
        currentTenant.TenantId = tenant.Id;

        // --- Studio 1: Hourly ---
        var studioA = new Studio
        {
            Name = "Downtown Dance Academy",
            Address = "120 Main St, Springfield",
            PayType = PayType.Hourly,
            PayRate = 45.00m
        };

        // --- Studio 2: PerHeadcount ---
        var studioB = new Studio
        {
            Name = "Riverside Ballet Collective",
            Address = "8 River Rd, Riverside",
            PayType = PayType.PerHeadcount,
            PayRate = 12.50m
        };

        db.Studios.AddRange(studioA, studioB);
        await db.SaveChangesAsync(ct);

        // --- Classes ---
        var classA1 = new DanceClass { StudioId = studioA.Id, Name = "Tuesday Intermediate Ballet" };
        var classA2 = new DanceClass { StudioId = studioA.Id, Name = "Thursday Beginner Jazz" };
        var classA3 = new DanceClass { StudioId = studioA.Id, Name = "Saturday Tap Fundamentals" };
        var classB1 = new DanceClass { StudioId = studioB.Id, Name = "Monday Pointe Technique" };
        var classB2 = new DanceClass { StudioId = studioB.Id, Name = "Wednesday Contemporary" };

        db.Classes.AddRange(classA1, classA2, classA3, classB1, classB2);
        await db.SaveChangesAsync(ct);

        // --- Routines (one per class) ---
        db.Routines.AddRange(
            new Routine { ClassId = classA1.Id, SongTitle = "Clair de Lune", Artist = "Claude Debussy", ChoreographyNotes = "Focus on port de bras and controlled adagio." },
            new Routine { ClassId = classA2.Id, SongTitle = "Sing, Sing, Sing", Artist = "Benny Goodman", ChoreographyNotes = "High-energy jazz square combinations." },
            new Routine { ClassId = classA3.Id, SongTitle = "Puttin' On the Ritz", Artist = "Irving Berlin", ChoreographyNotes = "Shuffle-ball-change drills, keep rhythm tight." },
            new Routine { ClassId = classB1.Id, SongTitle = "Swan Lake, Act II", Artist = "Pyotr Ilyich Tchaikovsky", ChoreographyNotes = "Relevé endurance; watch alignment over the box." },
            new Routine { ClassId = classB2.Id, SongTitle = "Experience", Artist = "Ludovico Einaudi", ChoreographyNotes = "Floor work and weight-sharing partnering." }
        );

        // --- Students: Studio A (6) ---
        var a1 = new Student { StudioId = studioA.Id, FirstName = "Ava", LastName = "Bennett", DateOfBirth = new DateOnly(2012, 3, 14), ParentName = "Maria Bennett", ParentEmail = "maria.bennett@example.com", ParentPhone = "555-0101" };
        var a2 = new Student { StudioId = studioA.Id, FirstName = "Liam", LastName = "Carter", DateOfBirth = new DateOnly(2011, 7, 2), ParentName = "James Carter", ParentEmail = "james.carter@example.com", ParentPhone = "555-0102", InjuryAlert = true, MovementModifications = "Recovering from a sprained ankle — no jumps or full relevé for 3 weeks." };
        var a3 = new Student { StudioId = studioA.Id, FirstName = "Sophia", LastName = "Diaz", DateOfBirth = new DateOnly(2013, 11, 20), ParentName = "Elena Diaz", ParentEmail = "elena.diaz@example.com", ParentPhone = "555-0103" };
        var a4 = new Student { StudioId = studioA.Id, FirstName = "Noah", LastName = "Evans", DateOfBirth = new DateOnly(2012, 1, 9), ParentName = "Rachel Evans", ParentEmail = "rachel.evans@example.com", ParentPhone = "555-0104" };
        var a5 = new Student { StudioId = studioA.Id, FirstName = "Mia", LastName = "Foster", DateOfBirth = new DateOnly(2010, 5, 30), ParentName = "David Foster", ParentEmail = "david.foster@example.com", ParentPhone = "555-0105" };
        var a6 = new Student { StudioId = studioA.Id, FirstName = "Ethan", LastName = "Grant", DateOfBirth = new DateOnly(2011, 9, 17), ParentName = "Laura Grant", ParentEmail = "laura.grant@example.com", ParentPhone = "555-0106" };

        // --- Students: Studio B (6) ---
        var b1 = new Student { StudioId = studioB.Id, FirstName = "Isabella", LastName = "Hughes", DateOfBirth = new DateOnly(2009, 4, 22), ParentName = "Karen Hughes", ParentEmail = "karen.hughes@example.com", ParentPhone = "555-0201" };
        var b2 = new Student { StudioId = studioB.Id, FirstName = "Lucas", LastName = "Ingram", DateOfBirth = new DateOnly(2010, 8, 11), ParentName = "Paul Ingram", ParentEmail = "paul.ingram@example.com", ParentPhone = "555-0202", InjuryAlert = true, MovementModifications = "Chronic knee issue — avoid deep pliés and repeated jumps; consult before pointe work." };
        var b3 = new Student { StudioId = studioB.Id, FirstName = "Charlotte", LastName = "Jensen", DateOfBirth = new DateOnly(2008, 12, 3), ParentName = "Anna Jensen", ParentEmail = "anna.jensen@example.com", ParentPhone = "555-0203" };
        var b4 = new Student { StudioId = studioB.Id, FirstName = "Henry", LastName = "Klein", DateOfBirth = new DateOnly(2009, 6, 28), ParentName = "Mark Klein", ParentEmail = "mark.klein@example.com", ParentPhone = "555-0204" };
        var b5 = new Student { StudioId = studioB.Id, FirstName = "Amelia", LastName = "Lopez", DateOfBirth = new DateOnly(2010, 2, 15), ParentName = "Sofia Lopez", ParentEmail = "sofia.lopez@example.com", ParentPhone = "555-0205" };
        var b6 = new Student { StudioId = studioB.Id, FirstName = "Jack", LastName = "Morgan", DateOfBirth = new DateOnly(2008, 10, 7), ParentName = "Diane Morgan", ParentEmail = "diane.morgan@example.com", ParentPhone = "555-0206" };

        db.Students.AddRange(a1, a2, a3, a4, a5, a6, b1, b2, b3, b4, b5, b6);
        await db.SaveChangesAsync(ct);

        // --- Recital participation rows ---
        db.RecitalParticipations.AddRange(
            new RecitalParticipation { StudentId = a1.Id, ClassId = classA1.Id, IsParticipating = true },
            new RecitalParticipation { StudentId = a2.Id, ClassId = classA1.Id, IsParticipating = false },
            new RecitalParticipation { StudentId = a3.Id, ClassId = classA1.Id, IsParticipating = true },
            new RecitalParticipation { StudentId = a4.Id, ClassId = classA2.Id, IsParticipating = true },
            new RecitalParticipation { StudentId = a5.Id, ClassId = classA2.Id, IsParticipating = true },
            new RecitalParticipation { StudentId = a6.Id, ClassId = classA3.Id, IsParticipating = false },
            new RecitalParticipation { StudentId = b1.Id, ClassId = classB1.Id, IsParticipating = true },
            new RecitalParticipation { StudentId = b2.Id, ClassId = classB1.Id, IsParticipating = false },
            new RecitalParticipation { StudentId = b3.Id, ClassId = classB1.Id, IsParticipating = true },
            new RecitalParticipation { StudentId = b4.Id, ClassId = classB2.Id, IsParticipating = true },
            new RecitalParticipation { StudentId = b5.Id, ClassId = classB2.Id, IsParticipating = true }
        );

        await db.SaveChangesAsync(ct);
    }
}
