namespace DanceManager.Api.Models;

/// <summary>
/// One number in the sequential recital show order. Usually tied to one of the
/// tenant's own <see cref="Routine"/>s, but may be a free-text "quick-add" entry
/// (RoutineId null, <see cref="Title"/> set) for a number that isn't the
/// teacher's class (e.g. a guest number). Numbers are grouped into named
/// <see cref="ShowSection"/>s (acts) and ordered within the running list.
/// </summary>
public class ShowProgram : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }

    /// <summary>The teacher's routine this number is, or null for a quick-add entry.</summary>
    public int? RoutineId { get; set; }

    /// <summary>Act/section this number belongs to; null = "Unassigned".</summary>
    public int? SectionId { get; set; }

    /// <summary>Studio this number belongs to (scopes standalone entries with no routine).</summary>
    public int? StudioId { get; set; }

    /// <summary>Display name for a standalone/quick-add number (ignored when RoutineId is set).</summary>
    public string? Title { get; set; }

    /// <summary>
    /// JSON array of studentIds performing in a standalone/quick-add number, so the
    /// teacher's dancers can be tracked for back-to-back changes. Ignored for
    /// routine-linked numbers (their dancers come from class recital participation).
    /// </summary>
    public string? StudentIds { get; set; }

    /// <summary>Order within the running list (display sorts by section OrderIndex, then this).</summary>
    public int OrderPosition { get; set; }

    public Routine? Routine { get; set; }
    public ShowSection? Section { get; set; }
}
