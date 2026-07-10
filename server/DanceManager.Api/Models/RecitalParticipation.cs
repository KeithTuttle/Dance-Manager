namespace DanceManager.Api.Models;

/// <summary>
/// Whether a student participates in a given class's recital.
/// Composite key (StudentId, ClassId).
/// </summary>
public class RecitalParticipation : ITenantScoped
{
    public int TenantId { get; set; }
    public int StudentId { get; set; }
    public int ClassId { get; set; }
    public bool IsParticipating { get; set; }

    public Student? Student { get; set; }
    public DanceClass? Class { get; set; }
}
