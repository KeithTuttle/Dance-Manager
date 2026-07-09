namespace DanceManager.Api.Models;

public class CostumeOption
{
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public Gender Gender { get; set; }
    public string? Description { get; set; }
    public string? PhotoLink { get; set; }
    public string? OptionLink { get; set; }

    public Routine? Routine { get; set; }
}
