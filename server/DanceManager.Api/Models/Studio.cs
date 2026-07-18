namespace DanceManager.Api.Models;

public class Studio : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public PayType PayType { get; set; }
    public decimal PayRate { get; set; }

    /// <summary>
    /// Season/archive flag: an archived studio (e.g. last year's recital or a
    /// finished production) is hidden from the studio picker and all dropdowns,
    /// but keeps its data and can be unarchived from Settings → Studios.
    /// </summary>
    public bool IsArchived { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<DanceClass> Classes { get; set; } = new List<DanceClass>();
}
