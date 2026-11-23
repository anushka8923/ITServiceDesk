namespace ITServiceDesk.API.DTOs;

public class AttachmentResponseDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int TicketId { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
}