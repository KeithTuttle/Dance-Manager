namespace DanceManager.Api.Models;

public class Student : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int StudioId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? ParentName { get; set; }
    public string? ParentEmail { get; set; }
    public string? ParentPhone { get; set; }
    public string? MedicalNotes { get; set; }
    public bool InjuryAlert { get; set; }
    public string? MovementModifications { get; set; }
    /// <summary>Optional; used to color-code dancers on the formation map. Null = unspecified.</summary>
    public Gender? Gender { get; set; }

    public Studio? Studio { get; set; }
    public ICollection<StudentNote> Notes { get; set; } = new List<StudentNote>();
}
