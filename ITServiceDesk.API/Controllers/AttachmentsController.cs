using System.Security.Claims;
using ITServiceDesk.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITServiceDesk.API.Controllers;

[Route("api/tickets/{ticketId}/attachments")]
[ApiController]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly ITicketService _ticketService;

    public AttachmentsController(
        IAttachmentService attachmentService,
        ITicketService ticketService)
    {
        _attachmentService = attachmentService;
        _ticketService = ticketService;
    }

    private string CurrentUserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    private bool CanAccessAllTickets =>
        User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    [HttpGet]
    public async Task<IActionResult> GetAttachments(int ticketId)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(
            ticketId, CurrentUserId, CanAccessAllTickets);

        if (ticket == null)
            return NotFound(new { Message = "Ticket not found." });

        var attachments = await _attachmentService
            .GetAttachmentsAsync(ticketId);

        return Ok(attachments);
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(
        int ticketId,
        IFormFile file)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var ticket = await _ticketService.GetTicketByIdAsync(
            ticketId, CurrentUserId, CanAccessAllTickets);

        if (ticket == null)
            return NotFound(new { Message = "Ticket not found." });

        try
        {
            var attachment = await _attachmentService
                .UploadAttachmentAsync(ticketId, file, CurrentUserId);

            return Ok(attachment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(
        int ticketId,
        int attachmentId)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(
            ticketId, CurrentUserId, CanAccessAllTickets);

        if (ticket == null)
            return NotFound(new { Message = "Ticket not found." });

        var attachment = await _attachmentService
            .GetAttachmentFileAsync(ticketId, attachmentId);

        if (attachment == null)
            return NotFound(new { Message = "Attachment file not found." });

        return File(
            attachment.Value.Content,
            attachment.Value.ContentType,
            attachment.Value.FileName);
    }
}
