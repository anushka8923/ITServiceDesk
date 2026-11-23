namespace ITServiceDesk.API.DTOs;

public class TicketAssignmentResponseDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string AssignedToUserId { get; set; } = string.Empty;
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
}