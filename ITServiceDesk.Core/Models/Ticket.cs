
namespace ITServiceDesk.Core.Models;

public class Ticket
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Foreign Keys
    public int CategoryId { get; set; }

    public int PriorityId { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    // Navigation Properties
    public Category? Category { get; set; }

    public Priority? Priority { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
}
