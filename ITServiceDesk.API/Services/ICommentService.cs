
using ITServiceDesk.API.DTOs;

namespace ITServiceDesk.API.Services;

public interface ICommentService
{
    Task<IEnumerable<TicketCommentResponseDto>> GetCommentsByTicketIdAsync(int ticketId);

    Task<TicketCommentResponseDto> AddCommentAsync(
        int ticketId, AddCommentDto dto, string userId);
}
