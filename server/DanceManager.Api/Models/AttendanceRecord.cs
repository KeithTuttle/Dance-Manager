namespace DanceManager.Api.Models;

public class AttendanceRecord : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int ClassId { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    /// <summary>Optional reason, e.g. for an excused absence.</summary>
    public string? Note { get; set; }

    public Student? Student { get; set; }
    public DanceClass? Class { get; set; }
}
