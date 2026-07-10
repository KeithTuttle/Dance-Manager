namespace DanceManager.Api.Models;

/// <summary>
/// A candidate in a mock audition. Scores is a JSON (jsonb) object mapping
/// skill name -> 1-5 score. Average is computed from Scores, not stored.
/// </summary>
public class AuditionCandidate : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int AuditionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Scores { get; set; } = "{}";
    public AuditionDecision Decision { get; set; }
    public string? Notes { get; set; }

    public Audition? Audition { get; set; }
}
