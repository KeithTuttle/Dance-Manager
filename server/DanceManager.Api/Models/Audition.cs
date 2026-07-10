namespace DanceManager.Api.Models;

/// <summary>
/// A mock audition. SkillColumns is a JSON (jsonb) array of the skill names
/// being scored for this audition (columns are configurable per audition).
/// </summary>
public class Audition : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int SpotsAvailable { get; set; }
    public string SkillColumns { get; set; } = "[]";

    public ICollection<AuditionCandidate> Candidates { get; set; } = new List<AuditionCandidate>();
}
