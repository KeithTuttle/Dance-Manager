namespace DanceManager.Api.Models;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int ClassId { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }

    public Student? Student { get; set; }
    public DanceClass? Class { get; set; }
}
