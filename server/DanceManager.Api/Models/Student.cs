namespace DanceManager.Api.Models;

public class Student : ITenantScoped
{
    public int TenantId { get; set; }
    public int Id { get; set; }
    public int StudioId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>
    /// A typed-in age, for when the birth date isn't known. Only used when
    /// <see cref="DateOfBirth"/> is null — it's a snapshot from whenever it was
    /// entered, not a birthday, so it doesn't advance on its own the way a
    /// DOB-derived age does.
    /// </summary>
    public int? AgeYears { get; set; }
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
