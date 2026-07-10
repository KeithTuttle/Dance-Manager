namespace DanceManager.Api.Models;

/// <summary>
/// A progression milestone (skill/goal) tracked for students, e.g. "Single pirouette".
/// Scoped to a studio, optionally narrowed to a specific class.
/// </summary>
public class Milestone
{
    public int Id { get; set; }
    public int StudioId { get; set; }
    public int? ClassId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Studio? Studio { get; set; }
    public DanceClass? Class { get; set; }
}
