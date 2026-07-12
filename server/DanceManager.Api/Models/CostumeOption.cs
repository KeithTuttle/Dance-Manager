namespace DanceManager.Api.Models;

public class CostumeOption : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public Gender Gender { get; set; }
    public string? Description { get; set; }
    public string? Accessories { get; set; }
    public string? Shoes { get; set; }
    public string? PhotoLink { get; set; }
    public string? OptionLink { get; set; }

    public Routine? Routine { get; set; }
}
