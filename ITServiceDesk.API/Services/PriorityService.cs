
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDesk.API.Services;

public class PriorityService : IPriorityService
{
    private readonly ApplicationDbContext _context;

    public PriorityService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Get all active priorities
    public async Task<IEnumerable<PriorityResponseDto>> GetAllPrioritiesAsync()
    {
        return await _context.Priorities
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .Select(p => new PriorityResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .ToListAsync();
    }

    // Get priority by ID
    public async Task<PriorityResponseDto?> GetPriorityByIdAsync(int id)
    {
        return await _context.Priorities
            .Where(p => p.Id == id && p.IsActive)
            .Select(p => new PriorityResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync();
    }
}
