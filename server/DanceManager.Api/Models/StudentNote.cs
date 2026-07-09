namespace DanceManager.Api.Models;

/// <summary>
/// Free-form placement/progress note for a student (e.g. "should move up a level").
/// Distinct from Student.MedicalNotes. Optionally scoped to a class.
/// </summary>
public class StudentNote
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int? ClassId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Student? Student { get; set; }
    public DanceClass? Class { get; set; }
}
