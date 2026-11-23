
using ITServiceDesk.API.DTOs;

namespace ITServiceDesk.API.Services;

public interface IAssignmentService
{
    Task<TicketAssignmentResponseDto> AssignTicketAsync(
        AssignTicketDto dto,
        string assignedByUserId);

    Task<IEnumerable<TicketAssignmentResponseDto>> GetAllAssignmentsAsync();

    Task<TicketAssignmentResponseDto> UnassignTicketAsync(
        int assignmentId,
        string unassignedByUserId);
}
