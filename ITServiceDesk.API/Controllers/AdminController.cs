
using ITServiceDesk.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDesk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetAdminDashboard()
    {
        var stats = await _adminService.GetDashboardStatsAsync();

        return Ok(stats);
    }

    [HttpGet("category-stats")]
    public async Task<IActionResult> GetCategoryStats()
    {
        var stats = await _adminService.GetCategoryTicketStatsAsync();

        return Ok(stats);
    }
}
