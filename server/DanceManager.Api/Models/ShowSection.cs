namespace DanceManager.Api.Models;

/// <summary>
/// A named section of the recital running order (e.g. "Act 1", "Act 2",
/// "Finale"). Per-studio; ordered by <see cref="OrderIndex"/>. Show numbers
/// reference a section via <see cref="ShowProgram.SectionId"/>.
/// </summary>
public class ShowSection : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int StudioId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
