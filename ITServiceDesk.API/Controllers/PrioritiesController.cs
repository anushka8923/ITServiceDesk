
using ITServiceDesk.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDesk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PrioritiesController : ControllerBase
{
    private readonly IPriorityService _priorityService;

    public PrioritiesController(IPriorityService priorityService)
    {
        _priorityService = priorityService;
    }

    // GET: api/priorities
    [HttpGet]
    public async Task<IActionResult> GetAllPriorities()
    {
        var priorities = await _priorityService.GetAllPrioritiesAsync();

        return Ok(priorities);
    }

    // GET: api/priorities/1
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPriorityById(int id)
    {
        var priority = await _priorityService.GetPriorityByIdAsync(id);

        if (priority == null)
        {
            return NotFound(new { Message = "Priority not found." });
        }

        return Ok(priority);
    }
}
