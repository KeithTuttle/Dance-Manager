namespace DanceManager.Api.Models;

/// <summary>
/// A staging formation for a routine. StudentCoordinates is a JSON (jsonb) object
/// mapping StudentId -> { x, y } percentage coordinates on the stage grid.
/// </summary>
public class Formation
{
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public string FormationName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string StudentCoordinates { get; set; } = "{}";

    public Routine? Routine { get; set; }
}
