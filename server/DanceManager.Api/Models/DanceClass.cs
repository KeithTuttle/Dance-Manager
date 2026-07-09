namespace DanceManager.Api.Models;

/// <summary>
/// A class taught at a studio (e.g. "Tuesday Intermediate Ballet").
/// Referenced pervasively across the app as ClassId.
/// </summary>
public class DanceClass
{
    public int Id { get; set; }
    public int StudioId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Studio? Studio { get; set; }
    public ICollection<Routine> Routines { get; set; } = new List<Routine>();
    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
    public ICollection<LessonPlanEntry> LessonPlanEntries { get; set; } = new List<LessonPlanEntry>();
}
