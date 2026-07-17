namespace DanceManager.Api.Models;

public class Routine : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? VideoUrl { get; set; }
    public string? ChoreographyNotes { get; set; }
    /// <summary>
    /// Costume worn in this number (free-text label, reused across numbers). Used to
    /// tell whether a dancer in two back-to-back numbers actually has to change:
    /// adjacent numbers sharing the same non-empty label need no quick change.
    /// </summary>
    public string? CostumeLabel { get; set; }

    public DanceClass? Class { get; set; }
    public ICollection<Formation> Formations { get; set; } = new List<Formation>();
}
