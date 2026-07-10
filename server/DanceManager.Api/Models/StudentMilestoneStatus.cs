namespace DanceManager.Api.Models;

/// <summary>
/// A single cell in the progression matrix: where a student stands on a milestone.
/// Unique per (StudentId, MilestoneId).
/// </summary>
public class StudentMilestoneStatus : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int MilestoneId { get; set; }
    public ProgressStatus Status { get; set; }

    public Student? Student { get; set; }
    public Milestone? Milestone { get; set; }
}
