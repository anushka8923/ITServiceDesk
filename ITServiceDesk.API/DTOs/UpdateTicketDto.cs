
using System.ComponentModel.DataAnnotations;

namespace ITServiceDesk.API.DTOs;

public class UpdateTicketDto
{
    [Required(ErrorMessage = "Ticket title is required.")]
    [StringLength(200, MinimumLength = 5,
        ErrorMessage = "Title must be between 5 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000, MinimumLength = 10,
        ErrorMessage = "Description must be between 10 and 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid category.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Priority is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid priority.")]
    public int PriorityId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Open|In Progress|Resolved|Closed)$",
        ErrorMessage = "Invalid ticket status.")]
    public string Status { get; set; } = "Open";
}
