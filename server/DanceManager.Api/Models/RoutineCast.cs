namespace DanceManager.Api.Models;

/// <summary>
/// A student cast in a specific routine (number). Unlike a class roster
/// (<see cref="Enrollment"/>) or a recital opt-in (<see cref="RecitalParticipation"/>),
/// this is the explicit, per-number cast: a hand-picked subset that can span
/// groups (e.g. a musical number that borrows dancers from another group).
/// A routine with any cast rows uses them as its dancer set; a routine with none
/// falls back to its class's recital participation. Composite key (RoutineId, StudentId).
/// </summary>
public class RoutineCast : ITenantScoped
{
    public int TenantId { get; set; }
    public int RoutineId { get; set; }
    public int StudentId { get; set; }

    public Routine? Routine { get; set; }
    public Student? Student { get; set; }
}
