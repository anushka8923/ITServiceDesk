
namespace ITServiceDesk.Core.Models;

public class Priority
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Property
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
