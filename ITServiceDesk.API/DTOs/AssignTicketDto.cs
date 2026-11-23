using System.ComponentModel.DataAnnotations;

namespace ITServiceDesk.API.DTOs;

public class AssignTicketDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int TicketId { get; set; }

    [Required]
    public string AssignedToUserId { get; set; } = string.Empty;
}