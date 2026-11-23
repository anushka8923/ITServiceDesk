
using System.Security.Claims;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDesk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IAuditService _auditService;

    public TicketsController(
        ITicketService ticketService,
        IAuditService auditService)
    {
        _ticketService = ticketService;
        _auditService = auditService;
    }

    private string CurrentUserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    private bool CanAccessAllTickets =>
        User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    [Authorize(Roles = "Admin")]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyTickets()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var tickets = await _ticketService.GetAllTicketsAsync(
            CurrentUserId, isAdmin: false);

        return Ok(tickets);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTickets()
    {
        var tickets = await _ticketService.GetAllTicketsAsync(
            CurrentUserId, CanAccessAllTickets);

        return Ok(tickets);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchTickets(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? categoryId,
        [FromQuery] int? priorityId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder)
    {
        var tickets = await _ticketService.SearchTicketsAsync(
            search, status, categoryId, priorityId,
            sortBy, sortOrder, CurrentUserId, CanAccessAllTickets);

        return Ok(tickets);
    }

    [HttpGet("paginated")]
    public async Task<IActionResult> GetPaginatedTickets(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 5)
    {
        var result = await _ticketService.GetPaginatedTicketsAsync(
            pageNumber, pageSize, CurrentUserId, CanAccessAllTickets);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTicketById(int id)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(
            id, CurrentUserId, CanAccessAllTickets);

        if (ticket == null)
            return NotFound(new { Message = "Ticket not found." });

        return Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTicket(CreateTicketDto dto)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        try
        {
            var ticket = await _ticketService.CreateTicketAsync(
                dto, CurrentUserId);

            await _auditService.LogActionAsync(
                CurrentUserId,
                "Ticket Created",
                $"Ticket ID {ticket.Id} was created.");

            return CreatedAtAction(
                nameof(GetTicketById),
                new { id = ticket.Id },
                ticket);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTicket(
        int id, UpdateTicketDto dto)
    {
        try
        {
            var updated = await _ticketService.UpdateTicketAsync(
                id, dto, CurrentUserId, CanAccessAllTickets);

            if (!updated)
                return NotFound(new { Message = "Ticket not found." });

            await _auditService.LogActionAsync(
                CurrentUserId,
                "Ticket Updated",
                $"Ticket ID {id} was updated.");

            return Ok(new { Message = "Ticket updated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateTicketStatus(
        int id, TicketStatusUpdateDto dto)
    {
        var updated = await _ticketService.UpdateTicketStatusAsync(id, dto);

        if (!updated)
            return NotFound(new { Message = "Ticket not found." });

        await _auditService.LogActionAsync(
            CurrentUserId,
            "Ticket Status Updated",
            $"Ticket ID {id} status changed to {dto.Status}.");

        return Ok(new { Message = "Ticket status updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTicket(int id)
    {
        var deleted = await _ticketService.DeleteTicketAsync(
            id, CurrentUserId, CanAccessAllTickets);

        if (!deleted)
            return NotFound(new { Message = "Ticket not found." });

        await _auditService.LogActionAsync(
            CurrentUserId,
            "Ticket Deleted",
            $"Ticket ID {id} was deleted.");

        return Ok(new { Message = "Ticket deleted successfully." });
    }
}
