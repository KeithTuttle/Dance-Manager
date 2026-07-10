namespace DanceManager.Api.Models;

public class LessonPlanEntry : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ClassId { get; set; }
    public DateOnly WeekOf { get; set; }
    public string? CoveredThisWeek { get; set; }
    public string? PlannedNextWeek { get; set; }
    public string? Notes { get; set; }

    public DanceClass? Class { get; set; }
}
