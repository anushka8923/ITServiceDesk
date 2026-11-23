
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDesk.API.Services;

public class AssignmentService : IAssignmentService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IEmailService? _emailService;

    // Main constructor used by dependency injection
    public AssignmentService(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IAuditService auditService,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _emailService = emailService;
    }

    // Backward-compatible constructor for existing unit tests
    public AssignmentService(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _emailService = null;
    }

    public async Task<TicketAssignmentResponseDto> AssignTicketAsync(
        AssignTicketDto dto,
        string assignedByUserId)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == dto.TicketId);

        if (ticket == null)
            throw new ArgumentException("Ticket not found.");

        var assignedUser = await _userManager.FindByIdAsync(
            dto.AssignedToUserId);

        if (assignedUser == null)
            throw new ArgumentException("Assigned user not found.");

        var existingAssignment = await _context.TicketAssignments
            .AnyAsync(ta =>
                ta.TicketId == dto.TicketId &&
                ta.UnassignedAt == null);

        if (existingAssignment)
            throw new TicketAssignmentConflictException();

        var assignment = new TicketAssignment
        {
            TicketId = dto.TicketId,
            AssignedToUserId = dto.AssignedToUserId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = DateTime.UtcNow
        };

        _context.TicketAssignments.Add(assignment);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _context.Entry(assignment).State = EntityState.Detached;
            throw new TicketAssignmentConflictException();
        }

        await _auditService.LogActionAsync(
            assignedByUserId,
            "Ticket Assigned",
            $"Ticket ID {dto.TicketId} assigned to user {dto.AssignedToUserId}."
        );

        // Send assignment email notification
        if (_emailService != null &&
            !string.IsNullOrWhiteSpace(assignedUser.Email))
        {
            await _emailService.SendEmailAsync(
                assignedUser.Email,
                "Ticket Assigned - IT Service Desk",
                $"Ticket ID {ticket.Id} has been assigned to you.\n\n" +
                $"Title: {ticket.Title}\n" +
                "Please log in to the IT Service Desk to view the ticket."
            );
        }

        return new TicketAssignmentResponseDto
        {
            Id = assignment.Id,
            TicketId = assignment.TicketId,
            AssignedToUserId = assignment.AssignedToUserId,
            AssignedByUserId = assignment.AssignedByUserId,
            AssignedAt = assignment.AssignedAt,
            UnassignedAt = assignment.UnassignedAt
        };
    }

    public async Task<IEnumerable<TicketAssignmentResponseDto>>
        GetAllAssignmentsAsync()
    {
        var assignments = await _context.TicketAssignments
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();

        return assignments.Select(ta => new TicketAssignmentResponseDto
        {
            Id = ta.Id,
            TicketId = ta.TicketId,
            AssignedToUserId = ta.AssignedToUserId,
            AssignedByUserId = ta.AssignedByUserId,
            AssignedAt = ta.AssignedAt,
            UnassignedAt = ta.UnassignedAt
        });
    }

    public async Task<TicketAssignmentResponseDto> UnassignTicketAsync(
        int assignmentId,
        string unassignedByUserId)
    {
        var assignment = await _context.TicketAssignments
            .FirstOrDefaultAsync(ta => ta.Id == assignmentId);

        if (assignment == null)
            throw new ArgumentException("Assignment not found.");

        if (assignment.UnassignedAt != null)
            throw new ArgumentException("Ticket is already unassigned.");

        assignment.UnassignedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(
            unassignedByUserId,
            "Ticket Unassigned",
            $"Assignment ID {assignmentId} for Ticket ID {assignment.TicketId} was unassigned."
        );

        return new TicketAssignmentResponseDto
        {
            Id = assignment.Id,
            TicketId = assignment.TicketId,
            AssignedToUserId = assignment.AssignedToUserId,
            AssignedByUserId = assignment.AssignedByUserId,
            AssignedAt = assignment.AssignedAt,
            UnassignedAt = assignment.UnassignedAt
        };
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (Exception? current = exception;
             current != null;
             current = current.InnerException)
        {
            if (current is SqlException sqlException &&
                (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class TicketAssignmentConflictException : ArgumentException
{
    public TicketAssignmentConflictException()
        : base("This ticket is already assigned to a user.")
    {
    }
}
