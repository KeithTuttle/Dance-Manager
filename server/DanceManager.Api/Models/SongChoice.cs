namespace DanceManager.Api.Models;

public class SongChoice : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? MusicCutNotes { get; set; }

    public Routine? Routine { get; set; }
}
