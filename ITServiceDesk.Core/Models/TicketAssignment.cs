namespace ITServiceDesk.Core.Models;

public class TicketAssignment
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public string AssignedToUserId { get; set; } = string.Empty;

    public string AssignedByUserId { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UnassignedAt { get; set; }

    // Navigation Property
    public Ticket? Ticket { get; set; }
}
