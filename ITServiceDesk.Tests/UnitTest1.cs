
using System;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.API.Services;
using ITServiceDesk.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ITServiceDesk.Tests;

public class TicketServiceTests
{
    private static bool LogStateContains(object? state, string text) =>
        state?.ToString()?.Contains(text) == true;

    private sealed class FailingSaveContext(
        DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public Exception? NextSaveFailure { get; set; }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (NextSaveFailure is { } exception)
            {
                NextSaveFailure = null;
                return Task.FromException<int>(exception);
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private ApplicationDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        context.Categories.AddRange(
            new Category { Id = 1, Name = "Hardware", IsActive = true },
            new Category { Id = 2, Name = "Software", IsActive = true }
        );

        context.Priorities.AddRange(
            new Priority { Id = 1, Name = "Low", IsActive = true },
            new Priority { Id = 2, Name = "High", IsActive = true }
        );

        context.Tickets.AddRange(
            new Ticket
            {
                Id = 1,
                Title = "User One Laptop Issue",
                Description = "First user laptop issue.",
                Status = "Open",
                CategoryId = 1,
                PriorityId = 1,
                CreatedByUserId = "user1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Ticket
            {
                Id = 2,
                Title = "User Two Software Issue",
                Description = "Second user software issue.",
                Status = "Resolved",
                CategoryId = 2,
                PriorityId = 2,
                CreatedByUserId = "user2",
                CreatedAt = DateTime.UtcNow
            }
        );

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task GetAllTicketsAsync_UserGetsOnlyOwnTickets()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);
        var result = await service.GetAllTicketsAsync("user1", false);

        Assert.Single(result);
        Assert.Equal("user1", result.First().CreatedByUserId);
    }

    [Fact]
    public async Task GetAllTicketsAsync_AdminGetsAllTickets()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);
        var result = await service.GetAllTicketsAsync("admin", true);

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task CreateTicketAsync_InvalidCategoryOrPriority_ThrowsException()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var dto = new CreateTicketDto
        {
            Title = "Test Ticket",
            Description = "This is a test ticket description.",
            CategoryId = 99,
            PriorityId = 99
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateTicketAsync(dto, "user1"));
    }

    [Fact]
    public async Task UpdateTicketAsync_UserCannotUpdateOtherUsersTicket()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var dto = new UpdateTicketDto
        {
            Title = "Updated Ticket",
            Description = "Updated ticket description.",
            CategoryId = 1,
            PriorityId = 1,
            Status = "Open"
        };

        var result = await service.UpdateTicketAsync(2, dto, "user1", false);
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateTicketAsync_AdminCanUpdateOtherUsersTicket()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var dto = new UpdateTicketDto
        {
            Title = "Admin Updated Ticket",
            Description = "Updated by admin.",
            CategoryId = 1,
            PriorityId = 1,
            Status = "In Progress"
        };

        var result = await service.UpdateTicketAsync(2, dto, "admin", true);

        Assert.True(result);
        var ticket = await context.Tickets.FindAsync(2);
        Assert.Equal("Admin Updated Ticket", ticket!.Title);
        Assert.Equal("In Progress", ticket.Status);
    }

    [Fact]
    public async Task UpdateTicketAsync_SuperAdminCanChangeTicketStatus()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);
        var dto = new UpdateTicketDto
        {
            Title = "SuperAdmin Updated Ticket",
            Description = "Updated by SuperAdmin.",
            CategoryId = 1,
            PriorityId = 1,
            Status = "Closed"
        };

        var result = await service.UpdateTicketAsync(2, dto, "superadmin", true);

        Assert.True(result);
        var ticket = await context.Tickets.FindAsync(2);
        Assert.Equal("Closed", ticket!.Status);
    }

    [Fact]
    public async Task DeleteTicketAsync_UserCannotDeleteOtherUsersTicket()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.DeleteTicketAsync(2, "user1", false);

        Assert.False(result);
        Assert.Equal(2, await context.Tickets.CountAsync());
    }

    [Fact]
    public async Task DeleteTicketAsync_AdminCanDeleteOtherUsersTicket()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.DeleteTicketAsync(2, "admin", true);

        Assert.True(result);
        Assert.Single(await context.Tickets.ToListAsync());
    }

    [Fact]
    public async Task SearchTicketsAsync_UserGetsOnlyMatchingTickets()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.SearchTicketsAsync(
            "Laptop", null, null, null, null, null, "user1", false);

        Assert.Single(result);
        Assert.Equal("User One Laptop Issue", result.First().Title);
        Assert.Equal("user1", result.First().CreatedByUserId);
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_ReturnsCorrectPage()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(1, 1, "admin", true);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_UserGetsOnlyOwnTickets()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(1, 10, "user1", false);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("user1", result.Items.First().CreatedByUserId);
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_ZeroPageNumberDefaultsToPageOne()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(0, 10, "admin", true);

        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_ZeroPageSizeDefaultsToOne()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(1, 0, "admin", true);

        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_ReturnsNewestTicketsFirst()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(1, 10, "admin", true);

        Assert.Equal("User Two Software Issue", result.Items.First().Title);
    }

    [Fact]
    public async Task SearchTicketsAsync_FiltersByStatus()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.SearchTicketsAsync(
            null, "Open", null, null, null, null, "admin", true);

        Assert.Single(result);
        Assert.Equal("Open", result.First().Status);
    }

    [Fact]
    public async Task SearchTicketsAsync_FiltersByCategory()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.SearchTicketsAsync(
            null, null, 2, null, null, null, "admin", true);

        Assert.Single(result);
        Assert.Equal(2, result.First().CategoryId);
    }

    [Fact]
    public async Task SearchTicketsAsync_FiltersByPriority()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.SearchTicketsAsync(
            null, null, null, 2, null, null, "admin", true);

        Assert.Single(result);
        Assert.Equal(2, result.First().PriorityId);
    }

    [Fact]
    public async Task SearchTicketsAsync_NoMatchingResults_ReturnsEmpty()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.SearchTicketsAsync(
            "NonexistentTicket", null, null, null, null, null, "admin", true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_ReturnsSecondPage()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(2, 1, "admin", true);

        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.CurrentPage);
        Assert.Single(result.Items);
        Assert.Equal("User One Laptop Issue", result.Items.First().Title);
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_PageSizeAbove100DefaultsTo100()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(1, 150, "admin", true);

        Assert.Equal(100, result.PageSize);
        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_NegativePageNumberDefaultsToOne()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(-5, 10, "admin", true);

        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task GetPaginatedTicketsAsync_NegativePageSizeDefaultsToOne()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.GetPaginatedTicketsAsync(1, -5, "admin", true);

        Assert.Equal(1, result.PageSize);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task CreateTicketAsync_ValidData_CreatesTicket()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var dto = new CreateTicketDto
        {
            Title = "New Laptop Request",
            Description = "My laptop needs a hardware repair.",
            CategoryId = 1,
            PriorityId = 2
        };

        var result = await service.CreateTicketAsync(dto, "user1");

        Assert.Equal("New Laptop Request", result.Title);
        Assert.Equal("user1", result.CreatedByUserId);
        Assert.Equal(3, await context.Tickets.CountAsync());
    }

    [Fact]
    public async Task UpdateTicketAsync_UserCanUpdateOwnTicket()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var dto = new UpdateTicketDto
        {
            Title = "Updated Laptop Issue",
            Description = "Updated description for my laptop issue.",
            CategoryId = 1,
            PriorityId = 2,
            Status = "In Progress"
        };

        var result = await service.UpdateTicketAsync(1, dto, "user1", false);

        Assert.True(result);
        var ticket = await context.Tickets.FindAsync(1);
        Assert.Equal("Updated Laptop Issue", ticket!.Title);
        Assert.Equal("Updated description for my laptop issue.", ticket.Description);
        Assert.Equal("Open", ticket.Status);
    }

    [Fact]
    public async Task DeleteTicketAsync_UserCanDeleteOwnTicket()
    {
        using var context = CreateTestContext();
        var service = new TicketService(context);

        var result = await service.DeleteTicketAsync(1, "user1", false);

        Assert.True(result);
        Assert.Null(await context.Tickets.FindAsync(1));
        Assert.Single(await context.Tickets.ToListAsync());
    }

    [Fact]
    public async Task DeleteTicketAsync_RemovesUnreferencedAttachmentFileAfterDatabaseDelete()
    {
        using var context = CreateTestContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var uploads = Path.Combine(folder, "Uploads");
        Directory.CreateDirectory(uploads);
        var physicalPath = Path.Combine(uploads, "ticket-file.pdf");
        await File.WriteAllTextAsync(physicalPath, "test");
        context.Attachments.Add(new Attachment
        {
            FileName = "ticket-file.pdf",
            FilePath = Path.Combine("Uploads", "ticket-file.pdf"),
            ContentType = "application/pdf",
            TicketId = 1,
            UploadedByUserId = "user1"
        });
        await context.SaveChangesAsync();

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(folder);
        var attachmentService = new AttachmentService(context, environment.Object);
        var ticketService = new TicketService(context, attachmentService);

        try
        {
            var deleted = await ticketService.DeleteTicketAsync(1, "user1", false);

            Assert.True(deleted);
            Assert.False(File.Exists(physicalPath));
            Assert.Empty(await context.Attachments.ToListAsync());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task DeleteTicketAsync_DatabaseFailure_LeavesAttachmentFileUntouched()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new FailingSaveContext(options);
        context.Categories.Add(new Category { Id = 1, Name = "Hardware", IsActive = true });
        context.Priorities.Add(new Priority { Id = 1, Name = "Medium", IsActive = true });
        context.Tickets.Add(new Ticket
        {
            Id = 1,
            Title = "Ticket",
            Description = "Ticket description",
            CategoryId = 1,
            PriorityId = 1,
            CreatedByUserId = "user1"
        });
        context.Attachments.Add(new Attachment
        {
            FileName = "ticket-file.pdf",
            FilePath = Path.Combine("Uploads", "ticket-file.pdf"),
            ContentType = "application/pdf",
            TicketId = 1,
            UploadedByUserId = "user1"
        });
        context.SaveChanges();

        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var uploads = Path.Combine(folder, "Uploads");
        Directory.CreateDirectory(uploads);
        var physicalPath = Path.Combine(uploads, "ticket-file.pdf");
        await File.WriteAllTextAsync(physicalPath, "keep");
        var expected = new InvalidOperationException("database delete failed");
        context.NextSaveFailure = expected;
        var attachmentService = new Mock<IAttachmentService>();
        var ticketService = new TicketService(context, attachmentService.Object);

        try
        {
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ticketService.DeleteTicketAsync(1, "user1", false));

            Assert.Same(expected, actual);
            Assert.True(File.Exists(physicalPath));
            attachmentService.Verify(
                service => service.DeleteFilesIfUnreferencedAsync(It.IsAny<IEnumerable<string>>()),
                Times.Never);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task DeleteTicketAsync_SharedAttachmentPath_IsRetained()
    {
        using var context = CreateTestContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var uploads = Path.Combine(folder, "Uploads");
        Directory.CreateDirectory(uploads);
        var physicalPath = Path.Combine(uploads, "shared.pdf");
        await File.WriteAllTextAsync(physicalPath, "shared");
        var sharedPath = Path.Combine("Uploads", "shared.pdf");
        context.Attachments.AddRange(
            new Attachment
            {
                FileName = "shared.pdf",
                FilePath = sharedPath,
                ContentType = "application/pdf",
                TicketId = 1,
                UploadedByUserId = "user1"
            },
            new Attachment
            {
                FileName = "shared.pdf",
                FilePath = sharedPath,
                ContentType = "application/pdf",
                TicketId = 2,
                UploadedByUserId = "user2"
            });
        await context.SaveChangesAsync();
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(folder);
        var attachmentService = new AttachmentService(context, environment.Object);
        var ticketService = new TicketService(context, attachmentService);

        try
        {
            Assert.True(await ticketService.DeleteTicketAsync(1, "admin", true));

            Assert.True(File.Exists(physicalPath));
            Assert.Contains(await context.Attachments.ToListAsync(),
                attachment => attachment.TicketId == 2 && attachment.FilePath == sharedPath);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task DeleteTicketAsync_CleanupFailure_IsLoggedAndDoesNotFailDeletion()
    {
        using var context = CreateTestContext();
        context.Attachments.Add(new Attachment
        {
            FileName = "cleanup-failure.pdf",
            FilePath = Path.Combine("Uploads", "cleanup-failure.pdf"),
            ContentType = "application/pdf",
            TicketId = 1,
            UploadedByUserId = "user1"
        });
        await context.SaveChangesAsync();

        var cleanupFailure = new IOException("file system unavailable");
        var attachmentService = new Mock<IAttachmentService>();
        attachmentService
            .Setup(service => service.DeleteFilesIfUnreferencedAsync(
                It.IsAny<IEnumerable<string>>()))
            .ThrowsAsync(cleanupFailure);
        var logger = new Mock<ILogger<TicketService>>();
        var ticketService = new TicketService(
            context,
            attachmentService.Object,
            logger.Object);

        var deleted = await ticketService.DeleteTicketAsync(1, "user1", false);

        Assert.True(deleted);
        Assert.Null(await context.Tickets.FindAsync(1));
        logger.Verify(log => log.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) =>
                LogStateContains(state, "Could not clean up attachment files")),
            cleanupFailure,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // CATEGORY SERVICE TESTS

    [Fact]
    public async Task GetAllCategoriesAsync_ReturnsOnlyActiveCategories()
    {
        using var context = CreateTestContext();
        context.Categories.Add(new Category
        {
            Id = 3,
            Name = "Inactive Category",
            IsActive = false
        });
        await context.SaveChangesAsync();

        var service = new CategoryService(context);
        var result = (await service.GetAllCategoriesAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task GetAllCategoriesAsync_ReturnsCategoriesAlphabetically()
    {
        using var context = CreateTestContext();
        var service = new CategoryService(context);

        var result = (await service.GetAllCategoriesAsync()).ToList();

        Assert.Equal("Hardware", result[0].Name);
        Assert.Equal("Software", result[1].Name);
    }

    [Fact]
    public async Task GetCategoryByIdAsync_ExistingActiveCategory_ReturnsCategory()
    {
        using var context = CreateTestContext();
        var service = new CategoryService(context);

        var result = await service.GetCategoryByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Hardware", result.Name);
    }

    [Fact]
    public async Task GetCategoryByIdAsync_InactiveOrMissingCategory_ReturnsNull()
    {
        using var context = CreateTestContext();
        context.Categories.Add(new Category
        {
            Id = 3,
            Name = "Inactive Category",
            IsActive = false
        });
        await context.SaveChangesAsync();

        var service = new CategoryService(context);

        Assert.Null(await service.GetCategoryByIdAsync(3));
        Assert.Null(await service.GetCategoryByIdAsync(99));
    }

    // PRIORITY SERVICE TESTS

    [Fact]
    public async Task GetAllPrioritiesAsync_ReturnsOnlyActivePriorities()
    {
        using var context = CreateTestContext();
        context.Priorities.Add(new Priority
        {
            Id = 3,
            Name = "Inactive Priority",
            IsActive = false
        });
        await context.SaveChangesAsync();

        var service = new PriorityService(context);
        var result = (await service.GetAllPrioritiesAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task GetAllPrioritiesAsync_ReturnsPrioritiesById()
    {
        using var context = CreateTestContext();
        var service = new PriorityService(context);

        var result = (await service.GetAllPrioritiesAsync()).ToList();

        Assert.Equal(1, result[0].Id);
        Assert.Equal(2, result[1].Id);
    }

    [Fact]
    public async Task GetPriorityByIdAsync_ExistingActivePriority_ReturnsPriority()
    {
        using var context = CreateTestContext();
        var service = new PriorityService(context);

        var result = await service.GetPriorityByIdAsync(2);

        Assert.NotNull(result);
        Assert.Equal("High", result.Name);
    }

    [Fact]
    public async Task GetPriorityByIdAsync_InactiveOrMissingPriority_ReturnsNull()
    {
        using var context = CreateTestContext();
        context.Priorities.Add(new Priority
        {
            Id = 3,
            Name = "Inactive Priority",
            IsActive = false
        });
        await context.SaveChangesAsync();

        var service = new PriorityService(context);

        Assert.Null(await service.GetPriorityByIdAsync(3));
        Assert.Null(await service.GetPriorityByIdAsync(99));
    }
}
