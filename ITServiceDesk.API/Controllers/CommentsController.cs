
using ITServiceDesk.API.DTOs;
using ITServiceDesk.API.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDesk.API.Controllers;

[Route("api/tickets/{ticketId}/comments")]
[ApiController]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _commentService;
    private readonly ITicketService _ticketService;

    public CommentsController(
        ICommentService commentService,
        ITicketService ticketService)
    {
        _commentService = commentService;
        _ticketService = ticketService;
    }

    private string CurrentUserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    private bool CanAccessAllTickets =>
        User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    // GET: api/tickets/1/comments
    [HttpGet]
    public async Task<IActionResult> GetComments(int ticketId)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(
            ticketId, CurrentUserId, CanAccessAllTickets);

        if (ticket == null)
        {
            return NotFound(new { Message = "Ticket not found." });
        }

        var comments = await _commentService
            .GetCommentsByTicketIdAsync(ticketId);

        return Ok(comments);
    }

    // POST: api/tickets/1/comments
    [HttpPost]
    public async Task<IActionResult> AddComment(
        int ticketId, AddCommentDto dto)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.GetTicketByIdAsync(
            ticketId, CurrentUserId, CanAccessAllTickets);

        if (ticket == null)
        {
            return NotFound(new { Message = "Ticket not found." });
        }

        try
        {
            var comment = await _commentService
                .AddCommentAsync(ticketId, dto, CurrentUserId);

            return Ok(comment);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }
}
