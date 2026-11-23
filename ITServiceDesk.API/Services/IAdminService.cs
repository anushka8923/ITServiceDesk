using ITServiceDesk.API.DTOs;

namespace ITServiceDesk.API.Services;

public interface IAdminService
{
    Task<object> GetDashboardStatsAsync();

    Task<IEnumerable<CategoryTicketStatsDto>> GetCategoryTicketStatsAsync();
}