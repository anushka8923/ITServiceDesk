
using ITServiceDesk.API.DTOs;

namespace ITServiceDesk.API.Services;

public interface IPriorityService
{
    Task<IEnumerable<PriorityResponseDto>> GetAllPrioritiesAsync();

    Task<PriorityResponseDto?> GetPriorityByIdAsync(int id);
}
