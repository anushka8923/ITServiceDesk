
using ITServiceDesk.API.DTOs;

namespace ITServiceDesk.API.Services;

public interface ITicketService
{
    Task<IEnumerable<TicketResponseDto>> GetAllTicketsAsync(
        string userId, bool isAdmin);

    Task<IEnumerable<TicketResponseDto>> SearchTicketsAsync(
        string? search,
        string? status,
        int? categoryId,
        int? priorityId,
        string? sortBy,
        string? sortOrder,
        string userId,
        bool isAdmin);

    Task<PaginatedResponseDto<TicketResponseDto>> GetPaginatedTicketsAsync(
        int pageNumber,
        int pageSize,
        string userId,
        bool isAdmin);

    Task<TicketResponseDto?> GetTicketByIdAsync(
        int id, string userId, bool isAdmin);

    Task<TicketResponseDto> CreateTicketAsync(
        CreateTicketDto dto, string userId);

    Task<bool> UpdateTicketAsync(
        int id,
        UpdateTicketDto dto,
        string userId,
        bool isAdmin);

    Task<bool> UpdateTicketStatusAsync(
        int id, TicketStatusUpdateDto dto);

    Task<bool> DeleteTicketAsync(
        int id, string userId, bool isAdmin);
}
