
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDesk.API.Services;

public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _context;

    public AdminService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<object> GetDashboardStatsAsync()
    {
        var totalTickets = await _context.Tickets.CountAsync();

        var openTickets = await _context.Tickets
            .CountAsync(t => t.Status == "Open");

        var inProgressTickets = await _context.Tickets
            .CountAsync(t => t.Status == "In Progress");

        var resolvedTickets = await _context.Tickets
            .CountAsync(t => t.Status == "Resolved");

        var closedTickets = await _context.Tickets
            .CountAsync(t => t.Status == "Closed");

        return new
        {
            TotalTickets = totalTickets,
            OpenTickets = openTickets,
            InProgressTickets = inProgressTickets,
            ResolvedTickets = resolvedTickets,
            ClosedTickets = closedTickets
        };
    }

    public async Task<IEnumerable<CategoryTicketStatsDto>>
        GetCategoryTicketStatsAsync()
    {
        return await _context.Tickets
            .GroupBy(t => t.Category!.Name)
            .Select(group => new CategoryTicketStatsDto
            {
                CategoryName = group.Key,
                TicketCount = group.Count()
            })
            .OrderByDescending(x => x.TicketCount)
            .ToListAsync();
    }
}
