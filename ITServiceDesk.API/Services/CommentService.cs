
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDesk.API.Services;

public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _context;

    public CommentService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Get all comments for a ticket
    public async Task<IEnumerable<TicketCommentResponseDto>> GetCommentsByTicketIdAsync(int ticketId)
    {
        return await _context.TicketComments
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TicketCommentResponseDto
            {
                Id = c.Id,
                Comment = c.Comment,
                CreatedAt = c.CreatedAt,
                TicketId = c.TicketId,
                UserId = c.UserId
            })
            .ToListAsync();
    }

    // Add a new comment
    public async Task<TicketCommentResponseDto> AddCommentAsync(
        int ticketId, AddCommentDto dto, string userId)
    {
        var ticketExists = await _context.Tickets
            .AnyAsync(t => t.Id == ticketId);

        if (!ticketExists)
        {
            throw new ArgumentException("Ticket not found.");
        }

        var comment = new TicketComment
        {
            Comment = dto.Comment,
            TicketId = ticketId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketComments.Add(comment);

        await _context.SaveChangesAsync();

        return new TicketCommentResponseDto
        {
            Id = comment.Id,
            Comment = comment.Comment,
            CreatedAt = comment.CreatedAt,
            TicketId = comment.TicketId,
            UserId = comment.UserId
        };
    }
}
