namespace DanceManager.Api.Models;

/// <summary>
/// A routine's position in the sequential recital show order.
/// </summary>
public class ShowProgram
{
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public int OrderPosition { get; set; }

    public Routine? Routine { get; set; }
}
