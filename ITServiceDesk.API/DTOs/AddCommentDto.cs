
using System.ComponentModel.DataAnnotations;

namespace ITServiceDesk.API.DTOs;

public class AddCommentDto
{
    [Required(ErrorMessage = "Comment is required.")]
    [StringLength(2000, MinimumLength = 2,
        ErrorMessage = "Comment must be between 2 and 2000 characters.")]
    public string Comment { get; set; } = string.Empty;
}
