
using ITServiceDesk.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDesk.API.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Priority> Priorities { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<TicketAssignment> TicketAssignments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ticket - Category
        builder.Entity<Ticket>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ticket - Priority
        builder.Entity<Ticket>()
            .HasOne(t => t.Priority)
            .WithMany(p => p.Tickets)
            .HasForeignKey(t => t.PriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ticket - Comments
        builder.Entity<TicketComment>()
            .HasOne(c => c.Ticket)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ticket - Attachments
        builder.Entity<Attachment>()
            .HasOne(a => a.Ticket)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ticket - Assignments
        builder.Entity<TicketAssignment>()
            .HasOne(a => a.Ticket)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TicketAssignment>()
            .HasIndex(a => a.TicketId)
            .IsUnique()
            .HasDatabaseName("IX_TicketAssignments_Active_TicketId")
            .HasFilter("[UnassignedAt] IS NULL");

        // Ticket - Created By User
        builder.Entity<Ticket>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Comment - User
        builder.Entity<TicketComment>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Attachment - Uploaded By User
        builder.Entity<Attachment>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Assignment - Assigned To User
        builder.Entity<TicketAssignment>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(a => a.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Assignment - Assigned By User
        builder.Entity<TicketAssignment>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(a => a.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Audit Log - User
        builder.Entity<AuditLog>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Field lengths
        builder.Entity<Ticket>()
            .Property(t => t.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Entity<Category>()
            .Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Entity<Priority>()
            .Property(p => p.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.Entity<TicketComment>()
            .Property(c => c.Comment)
            .HasMaxLength(2000)
            .IsRequired();

        // Seed Default Categories
        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Hardware", Description = "Computer, laptop and printer issues" },
            new Category { Id = 2, Name = "Software", Description = "Software installation and application issues" },
            new Category { Id = 3, Name = "Network", Description = "Internet and network connectivity issues" },
            new Category { Id = 4, Name = "Account", Description = "Login and account access issues" },
            new Category { Id = 5, Name = "Other", Description = "Other technical issues" }
        );

        // Seed Default Priorities
        builder.Entity<Priority>().HasData(
            new Priority { Id = 1, Name = "Low", Description = "Minor issue" },
            new Priority { Id = 2, Name = "Medium", Description = "Normal issue" },
            new Priority { Id = 3, Name = "High", Description = "Important issue" },
            new Priority { Id = 4, Name = "Critical", Description = "Urgent issue requiring immediate attention" }
        );
    }
}
