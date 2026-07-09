namespace DanceManager.Api.Models;

public class Routine
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? VideoUrl { get; set; }
    public string? ChoreographyNotes { get; set; }

    public DanceClass? Class { get; set; }
    public ICollection<Formation> Formations { get; set; } = new List<Formation>();
}
