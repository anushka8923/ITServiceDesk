
namespace ITServiceDesk.Core.Models;

public class TicketComment
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int TicketId { get; set; }

    // Navigation Property
    public Ticket? Ticket { get; set; }

    public string UserId { get; set; } = string.Empty;
}
