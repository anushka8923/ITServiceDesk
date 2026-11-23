
namespace ITServiceDesk.API.DTOs;

public class TicketCommentResponseDto
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public int TicketId { get; set; }

    public string UserId { get; set; } = string.Empty;
}
