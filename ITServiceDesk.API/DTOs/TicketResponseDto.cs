
namespace ITServiceDesk.API.DTOs;

public class TicketResponseDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public int PriorityId { get; set; }

    public string PriorityName { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;
}
