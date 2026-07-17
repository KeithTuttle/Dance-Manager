using System.Reflection;
using DanceManager.Api.Auth;
using DanceManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenant _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant tenant) : base(options)
        => _tenant = tenant;

    // Tenancy tables (global — NOT tenant-scoped).
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Studio> Studios => Set<Studio>();
    public DbSet<DanceClass> Classes => Set<DanceClass>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentNote> StudentNotes => Set<StudentNote>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<LessonPlanEntry> LessonPlanEntries => Set<LessonPlanEntry>();
    public DbSet<Routine> Routines => Set<Routine>();
    public DbSet<Formation> Formations => Set<Formation>();
    public DbSet<RecitalParticipation> RecitalParticipations => Set<RecitalParticipation>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<RoutineCast> RoutineCasts => Set<RoutineCast>();
    public DbSet<ShowProgram> ShowPrograms => Set<ShowProgram>();
    public DbSet<ShowSection> ShowSections => Set<ShowSection>();
    public DbSet<CostumeRecord> CostumeRecords => Set<CostumeRecord>();
    public DbSet<SongChoice> SongChoices => Set<SongChoice>();
    public DbSet<CostumeOption> CostumeOptions => Set<CostumeOption>();
    public DbSet<Audition> Auditions => Set<Audition>();
    public DbSet<AuditionCandidate> AuditionCandidates => Set<AuditionCandidate>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<StudentMilestoneStatus> StudentMilestoneStatuses => Set<StudentMilestoneStatus>();

    /// <summary>Tenant used by the query filter; 0 (matches nothing) when unresolved.</summary>
    private int CurrentTenantId => _tenant.TenantId ?? 0;

    private static readonly MethodInfo ConfigureTenantFilterMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureTenantScope), BindingFlags.Instance | BindingFlags.NonPublic)!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Store enums as strings for readability in the database.
        b.Entity<Studio>().Property(x => x.PayType).HasConversion<string>();
        b.Entity<Studio>().Property(x => x.PayRate).HasColumnType("numeric(10,2)");
        b.Entity<AttendanceRecord>().Property(x => x.Status).HasConversion<string>();
        b.Entity<CostumeRecord>().Property(x => x.OrderStatus).HasConversion<string>();
        b.Entity<CostumeRecord>().Property(x => x.FeeAmount).HasColumnType("numeric(10,2)");
        b.Entity<CostumeOption>().Property(x => x.Gender).HasConversion<string>();
        b.Entity<Student>().Property(x => x.Gender).HasConversion<string>();
        b.Entity<AuditionCandidate>().Property(x => x.Decision).HasConversion<string>();
        b.Entity<StudentMilestoneStatus>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Membership>().Property(x => x.Role).HasConversion<string>();

        // JSON columns (Postgres jsonb).
        b.Entity<Formation>().Property(x => x.StudentCoordinates).HasColumnType("jsonb");
        b.Entity<ShowProgram>().Property(x => x.StudentIds).HasColumnType("jsonb");
        b.Entity<Audition>().Property(x => x.SkillColumns).HasColumnType("jsonb");
        b.Entity<AuditionCandidate>().Property(x => x.Scores).HasColumnType("jsonb");

        // Composite keys.
        b.Entity<RecitalParticipation>().HasKey(x => new { x.StudentId, x.ClassId });
        b.Entity<Enrollment>().HasKey(x => new { x.StudentId, x.ClassId });
        b.Entity<RoutineCast>().HasKey(x => new { x.RoutineId, x.StudentId });

        // One Clerk user maps to exactly one membership.
        b.Entity<Membership>().HasIndex(x => x.ClerkUserId).IsUnique();

        // Prevent multiple cascade paths; keep deletes explicit for feature agents.
        b.Entity<AttendanceRecord>()
            .HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<StudentNote>()
            .HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.SetNull);
        // Deleting a section must NOT delete its numbers — they fall back to
        // "Unassigned" (SectionId null).
        b.Entity<ShowProgram>()
            .HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.SetNull);
        // RoutineId is now optional (quick-add entries have none). Keep the delete
        // behavior Cascade — deleting a routine still removes its show entry —
        // rather than the SetNull EF would otherwise default to for an optional FK.
        b.Entity<ShowProgram>()
            .HasOne(x => x.Routine).WithMany().HasForeignKey(x => x.RoutineId)
            .OnDelete(DeleteBehavior.Cascade);

        // Helpful uniqueness / lookup indexes.
        b.Entity<AttendanceRecord>().HasIndex(x => new { x.StudentId, x.ClassId, x.Date }).IsUnique();
        b.Entity<ClassSession>().HasIndex(x => new { x.ClassId, x.Date }).IsUnique();
        b.Entity<ShowProgram>().HasIndex(x => x.OrderPosition);
        b.Entity<StudentMilestoneStatus>().HasIndex(x => new { x.StudentId, x.MilestoneId }).IsUnique();

        // Tenant isolation: every ITenantScoped entity gets a global query filter
        // (e.TenantId == current tenant) and a TenantId index. Defense-in-depth —
        // a query that forgets to filter still cannot cross tenants.
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                ConfigureTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { b });
        }
    }

    private void ConfigureTenantScope<T>(ModelBuilder b) where T : class, ITenantScoped
    {
        b.Entity<T>().HasIndex(e => e.TenantId);
        // References the context instance member so EF re-evaluates it per query
        // rather than baking a constant into the cached model.
        b.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        ResetStoreGeneratedKeys();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        ResetStoreGeneratedKeys();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Clears any client-supplied value on a store-generated (identity) primary key
    /// before insert, so the database always assigns the id. Controllers bind whole
    /// entities from request bodies, and the client sends a temporary negative id for
    /// optimistic UI rows; without this, Postgres' <c>IDENTITY BY DEFAULT</c> honors
    /// that value — inserting a negative PK, then failing later inserts with a
    /// duplicate-key (23505) violation. Composite-key join rows (Enrollment,
    /// RecitalParticipation) have no store-generated key and are left untouched.
    /// </summary>
    private void ResetStoreGeneratedKeys()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            var key = entry.Metadata.FindPrimaryKey();
            if (key is null) continue;
            foreach (var prop in key.Properties)
            {
                if (prop.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd
                    && (prop.ClrType == typeof(int) || prop.ClrType == typeof(long)))
                {
                    entry.Property(prop.Name).CurrentValue = Activator.CreateInstance(prop.ClrType);
                }
            }
        }
    }

    /// <summary>Stamp the current tenant onto new tenant-scoped rows on insert.</summary>
    private void StampTenant()
    {
        var tid = _tenant.TenantId;
        if (tid is null or 0) return;
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == 0)
                entry.Entity.TenantId = tid.Value;
        }
    }
}
