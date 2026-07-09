namespace DanceManager.Api.Models;

/// <summary>
/// A dated session of a class, carrying Markdown class notes for that date.
/// </summary>
public class ClassSession
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }

    public DanceClass? Class { get; set; }
}
