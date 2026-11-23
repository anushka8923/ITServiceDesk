
using System.ComponentModel.DataAnnotations;

namespace ITServiceDesk.API.DTOs;

public class TicketStatusUpdateDto
{
    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Open|In Progress|Resolved|Closed)$",
        ErrorMessage = "Status must be Open, In Progress, Resolved, or Closed.")]
    public string Status { get; set; } = string.Empty;
}
