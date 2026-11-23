
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDesk.API.Services;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser>? _userManager;
    private readonly IEmailService? _emailService;
    private readonly ILogger<TicketService>? _logger;
    private readonly IAttachmentService? _attachmentService;

    // Main constructor used by application dependency injection
    public TicketService(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IEmailService emailService,
        ILogger<TicketService> logger,
        IAttachmentService attachmentService)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
        _attachmentService = attachmentService;
    }

    // Backward-compatible constructor for existing unit tests
    public TicketService(
        ApplicationDbContext context,
        IAttachmentService? attachmentService = null,
        ILogger<TicketService>? logger = null)
    {
        _context = context;
        _userManager = null;
        _emailService = null;
        _logger = logger;
        _attachmentService = attachmentService;
    }

    private IQueryable<Ticket> GetAccessibleTickets(
        string userId, bool isAdmin)
    {
        var query = _context.Tickets.AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(t => t.CreatedByUserId == userId);
        }

        return query;
    }

    public async Task<IEnumerable<TicketResponseDto>> GetAllTicketsAsync(
        string userId, bool isAdmin)
    {
        return await GetAccessibleTickets(userId, isAdmin)
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TicketResponseDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CategoryId = t.CategoryId,
                CategoryName = t.Category!.Name,
                PriorityId = t.PriorityId,
                PriorityName = t.Priority!.Name,
                CreatedByUserId = t.CreatedByUserId
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketResponseDto>> SearchTicketsAsync(
        string? search,
        string? status,
        int? categoryId,
        int? priorityId,
        string? sortBy,
        string? sortOrder,
        string userId,
        bool isAdmin)
    {
        var query = GetAccessibleTickets(userId, isAdmin)
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                t.Title.Contains(search) ||
                t.Description.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId.Value);

        if (priorityId.HasValue)
            query = query.Where(t => t.PriorityId == priorityId.Value);

        var ascending = string.Equals(
            sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        query = (sortBy?.ToLower()) switch
        {
            "title" => ascending
                ? query.OrderBy(t => t.Title)
                : query.OrderByDescending(t => t.Title),

            "status" => ascending
                ? query.OrderBy(t => t.Status)
                : query.OrderByDescending(t => t.Status),

            "priority" => ascending
                ? query.OrderBy(t => t.Priority!.Name)
                : query.OrderByDescending(t => t.Priority!.Name),

            "createdat" => ascending
                ? query.OrderBy(t => t.CreatedAt)
                : query.OrderByDescending(t => t.CreatedAt),

            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        return await query.Select(t => new TicketResponseDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            CategoryId = t.CategoryId,
            CategoryName = t.Category!.Name,
            PriorityId = t.PriorityId,
            PriorityName = t.Priority!.Name,
            CreatedByUserId = t.CreatedByUserId
        }).ToListAsync();
    }

    public async Task<PaginatedResponseDto<TicketResponseDto>>
        GetPaginatedTicketsAsync(
            int pageNumber,
            int pageSize,
            string userId,
            bool isAdmin)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = GetAccessibleTickets(userId, isAdmin);
        var totalCount = await query.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pageSize);

        var tickets = await query
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketResponseDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CategoryId = t.CategoryId,
                CategoryName = t.Category!.Name,
                PriorityId = t.PriorityId,
                PriorityName = t.Priority!.Name,
                CreatedByUserId = t.CreatedByUserId
            })
            .ToListAsync();

        return new PaginatedResponseDto<TicketResponseDto>
        {
            TotalCount = totalCount,
            CurrentPage = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = tickets
        };
    }

    public async Task<TicketResponseDto?> GetTicketByIdAsync(
        int id, string userId, bool isAdmin)
    {
        return await GetAccessibleTickets(userId, isAdmin)
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Where(t => t.Id == id)
            .Select(t => new TicketResponseDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CategoryId = t.CategoryId,
                CategoryName = t.Category!.Name,
                PriorityId = t.PriorityId,
                PriorityName = t.Priority!.Name,
                CreatedByUserId = t.CreatedByUserId
            })
            .FirstOrDefaultAsync();
    }

    public async Task<TicketResponseDto> CreateTicketAsync(
        CreateTicketDto dto, string userId)
    {
        var category = await _context.Categories.FindAsync(dto.CategoryId);
        var priority = await _context.Priorities.FindAsync(dto.PriorityId);

        if (category == null || priority == null)
            throw new ArgumentException("Invalid category or priority.");

        var ticket = new Ticket
        {
            Title = dto.Title,
            Description = dto.Description,
            CategoryId = dto.CategoryId,
            PriorityId = dto.PriorityId,
            CreatedByUserId = userId,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        await SendTicketNotificationAsync(
            ticket.CreatedByUserId,
            $"Ticket #{ticket.Id} Created",
            $"Hello,\n\nYour ticket has been created successfully.\n\n" +
            $"Ticket ID: {ticket.Id}\n" +
            $"Title: {ticket.Title}\n" +
            $"Status: {ticket.Status}\n\n" +
            "Regards,\nIT Service Desk Team"
        );

        return new TicketResponseDto
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            CategoryId = ticket.CategoryId,
            CategoryName = category.Name,
            PriorityId = ticket.PriorityId,
            PriorityName = priority.Name,
            CreatedByUserId = ticket.CreatedByUserId
        };
    }

    public async Task<bool> UpdateTicketAsync(
        int id,
        UpdateTicketDto dto,
        string userId,
        bool isAdmin)
    {
        var ticket = await GetAccessibleTickets(userId, isAdmin)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return false;

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == dto.CategoryId);

        var priorityExists = await _context.Priorities
            .AnyAsync(p => p.Id == dto.PriorityId);

        if (!categoryExists || !priorityExists)
            throw new ArgumentException("Invalid category or priority.");

        ticket.Title = dto.Title;
        ticket.Description = dto.Description;
        ticket.CategoryId = dto.CategoryId;
        ticket.PriorityId = dto.PriorityId;
        if (isAdmin)
        {
            ticket.Status = dto.Status;
        }
        ticket.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await SendTicketNotificationAsync(
            ticket.CreatedByUserId,
            $"Ticket #{ticket.Id} Updated",
            $"Hello,\n\nYour ticket details have been updated.\n\n" +
            $"Ticket ID: {ticket.Id}\n" +
            $"Title: {ticket.Title}\n" +
            $"Status: {ticket.Status}\n\n" +
            "Regards,\nIT Service Desk Team"
        );

        return true;
    }

    public async Task<bool> UpdateTicketStatusAsync(
        int id, TicketStatusUpdateDto dto)
    {
        var ticket = await _context.Tickets.FindAsync(id);

        if (ticket == null)
            return false;

        var oldStatus = ticket.Status;

        ticket.Status = dto.Status;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (oldStatus != ticket.Status)
        {
            await SendTicketNotificationAsync(
                ticket.CreatedByUserId,
                $"Ticket #{ticket.Id} Status Updated",
                $"Hello,\n\nYour ticket status has changed.\n\n" +
                $"Ticket ID: {ticket.Id}\n" +
                $"Title: {ticket.Title}\n" +
                $"Previous Status: {oldStatus}\n" +
                $"New Status: {ticket.Status}\n\n" +
                "Regards,\nIT Service Desk Team"
            );
        }

        return true;
    }

    public async Task<bool> DeleteTicketAsync(
        int id, string userId, bool isAdmin)
    {
        var ticket = await GetAccessibleTickets(userId, isAdmin)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return false;

        var ticketAttachments = await _context.Attachments
            .Where(attachment => attachment.TicketId == id)
            .ToListAsync();
        var attachmentPaths = ticketAttachments
            .Select(attachment => attachment.FilePath)
            .ToList();

        _context.Tickets.Remove(ticket);
        await _context.SaveChangesAsync();

        if (_attachmentService != null && attachmentPaths.Count > 0)
        {
            try
            {
                await _attachmentService.DeleteFilesIfUnreferencedAsync(attachmentPaths);
            }
            catch (Exception exception)
            {
                // Database deletion has succeeded; filesystem cleanup is best effort.
                _logger?.LogError(
                    exception,
                    "Could not clean up attachment files after deleting ticket {TicketId}.",
                    id);
            }
        }

        return true;
    }

    private async Task SendTicketNotificationAsync(
        string userId,
        string subject,
        string body)
    {
        // Unit tests using the old constructor skip email notifications
        if (_userManager == null || _emailService == null)
            return;

        try
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (string.IsNullOrWhiteSpace(user?.Email))
            {
                _logger?.LogWarning(
                    "Ticket notification skipped. Email not found for user {UserId}",
                    userId);
                return;
            }

            await _emailService.SendEmailAsync(
                user.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Ticket notification failed for user {UserId}", userId);
        }
    }
}
