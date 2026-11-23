
using System.Security.Claims;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDesk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpPost]
    public async Task<IActionResult> AssignTicket(AssignTicketDto dto)
    {
        var adminUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(adminUserId))
            return Unauthorized();

        try
        {
            var assignment = await _assignmentService.AssignTicketAsync(
                dto, adminUserId);

            return Ok(assignment);
        }
        catch (TicketAssignmentConflictException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignments()
    {
        var assignments = await _assignmentService.GetAllAssignmentsAsync();

        return Ok(assignments);
    }

    [HttpPatch("{id}/unassign")]
    public async Task<IActionResult> UnassignTicket(int id)
    {
        var adminUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(adminUserId))
            return Unauthorized();

        try
        {
            var assignment = await _assignmentService.UnassignTicketAsync(
                id, adminUserId);

            return Ok(assignment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
