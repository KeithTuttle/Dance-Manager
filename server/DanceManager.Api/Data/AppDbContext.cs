using DanceManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
    public DbSet<ShowProgram> ShowPrograms => Set<ShowProgram>();
    public DbSet<CostumeRecord> CostumeRecords => Set<CostumeRecord>();
    public DbSet<SongChoice> SongChoices => Set<SongChoice>();
    public DbSet<CostumeOption> CostumeOptions => Set<CostumeOption>();
    public DbSet<Audition> Auditions => Set<Audition>();
    public DbSet<AuditionCandidate> AuditionCandidates => Set<AuditionCandidate>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<StudentMilestoneStatus> StudentMilestoneStatuses => Set<StudentMilestoneStatus>();

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
        b.Entity<AuditionCandidate>().Property(x => x.Decision).HasConversion<string>();
        b.Entity<StudentMilestoneStatus>().Property(x => x.Status).HasConversion<string>();

        // JSON columns (Postgres jsonb).
        b.Entity<Formation>().Property(x => x.StudentCoordinates).HasColumnType("jsonb");
        b.Entity<Audition>().Property(x => x.SkillColumns).HasColumnType("jsonb");
        b.Entity<AuditionCandidate>().Property(x => x.Scores).HasColumnType("jsonb");

        // Composite key.
        b.Entity<RecitalParticipation>().HasKey(x => new { x.StudentId, x.ClassId });

        // Prevent multiple cascade paths; keep deletes explicit for feature agents.
        b.Entity<AttendanceRecord>()
            .HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<StudentNote>()
            .HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.SetNull);

        // Helpful uniqueness / lookup indexes.
        b.Entity<AttendanceRecord>().HasIndex(x => new { x.StudentId, x.ClassId, x.Date }).IsUnique();
        b.Entity<ClassSession>().HasIndex(x => new { x.ClassId, x.Date }).IsUnique();
        b.Entity<ShowProgram>().HasIndex(x => x.OrderPosition);
        b.Entity<StudentMilestoneStatus>().HasIndex(x => new { x.StudentId, x.MilestoneId }).IsUnique();
    }
}
