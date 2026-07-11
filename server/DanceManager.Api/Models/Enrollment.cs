namespace DanceManager.Api.Models;

/// <summary>
/// A student's membership in a class — the class roster. Distinct from
/// <see cref="RecitalParticipation"/> (a per-class recital opt-in): a student
/// can be enrolled in a class without participating in its recital.
/// Composite key (StudentId, ClassId).
/// </summary>
public class Enrollment : ITenantScoped
{
    public int TenantId { get; set; }
    public int StudentId { get; set; }
    public int ClassId { get; set; }

    public Student? Student { get; set; }
    public DanceClass? Class { get; set; }
}
