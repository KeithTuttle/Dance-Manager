namespace DanceManager.Api.Models;

public class CostumeRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string? CostumeSize { get; set; }
    public decimal FeeAmount { get; set; }
    public bool IsPaid { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public string? AlterationNotes { get; set; }

    public Student? Student { get; set; }
}
