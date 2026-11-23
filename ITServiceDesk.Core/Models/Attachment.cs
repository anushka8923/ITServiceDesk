namespace ITServiceDesk.Core.Models;

public class Attachment
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int TicketId { get; set; }

    // Navigation Properties
    public Ticket? Ticket { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;
}
