
using System;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.API.Services;
using ITServiceDesk.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ITServiceDesk.Tests;

public class AssignmentServiceTests
{
    private ApplicationDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        context.Categories.Add(new Category
        {
            Id = 1,
            Name = "Hardware",
            IsActive = true
        });

        context.Priorities.Add(new Priority
        {
            Id = 1,
            Name = "Low",
            IsActive = true
        });

        context.Tickets.Add(new Ticket
        {
            Id = 1,
            Title = "Laptop Issue",
            Description = "Laptop is not working properly.",
            Status = "Open",
            CategoryId = 1,
            PriorityId = 1,
            CreatedByUserId = "user1",
            CreatedAt = DateTime.UtcNow
        });

        context.SaveChanges();

        return context;
    }

    private Mock<UserManager<IdentityUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<IdentityUser>>();

        var userManager = new Mock<UserManager<IdentityUser>>(
            store.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<IdentityUser>>(),
            Array.Empty<IUserValidator<IdentityUser>>(),
            Array.Empty<IPasswordValidator<IdentityUser>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<IdentityUser>>>()
        );

        return userManager;
    }

    private AssignmentService CreateService(
        ApplicationDbContext context,
        Mock<UserManager<IdentityUser>> userManager,
        Mock<IAuditService> auditService)
    {
        return new AssignmentService(
            context,
            userManager.Object,
            auditService.Object
        );
    }

    private AssignTicketDto CreateAssignmentDto()
    {
        return new AssignTicketDto
        {
            TicketId = 1,
            AssignedToUserId = "user2"
        };
    }

    [Fact]
    public async Task AssignTicketAsync_ValidData_CreatesAssignment()
    {
        using var context = CreateTestContext();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        userManager
            .Setup(x => x.FindByIdAsync("user2"))
            .ReturnsAsync(new IdentityUser { Id = "user2" });

        auditService
            .Setup(x => x.LogActionAsync(
                "admin",
                "Ticket Assigned",
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(context, userManager, auditService);

        var result = await service.AssignTicketAsync(
            CreateAssignmentDto(), "admin");

        Assert.Equal(1, result.TicketId);
        Assert.Equal("user2", result.AssignedToUserId);
        Assert.Equal("admin", result.AssignedByUserId);
        Assert.True(result.Id > 0);
        Assert.Null(result.UnassignedAt);

        auditService.Verify(
            x => x.LogActionAsync(
                "admin",
                "Ticket Assigned",
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task AssignTicketAsync_InvalidTicket_ThrowsException()
    {
        using var context = CreateTestContext();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        var service = CreateService(context, userManager, auditService);

        var dto = CreateAssignmentDto();
        dto.TicketId = 99;

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AssignTicketAsync(dto, "admin"));

        userManager.Verify(
            x => x.FindByIdAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignTicketAsync_InvalidUser_ThrowsException()
    {
        using var context = CreateTestContext();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        userManager
            .Setup(x => x.FindByIdAsync("user2"))
            .ReturnsAsync((IdentityUser?)null);

        var service = CreateService(context, userManager, auditService);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AssignTicketAsync(
                CreateAssignmentDto(), "admin"));

        Assert.Empty(context.TicketAssignments);
    }

    [Fact]
    public async Task AssignTicketAsync_AlreadyAssignedTicket_ThrowsException()
    {
        using var context = CreateTestContext();

        context.TicketAssignments.Add(new TicketAssignment
        {
            Id = 1,
            TicketId = 1,
            AssignedToUserId = "user2",
            AssignedByUserId = "admin",
            AssignedAt = DateTime.UtcNow,
            UnassignedAt = null
        });

        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        userManager
            .Setup(x => x.FindByIdAsync("user2"))
            .ReturnsAsync(new IdentityUser { Id = "user2" });

        var service = CreateService(context, userManager, auditService);

        await Assert.ThrowsAsync<TicketAssignmentConflictException>(
            () => service.AssignTicketAsync(
                CreateAssignmentDto(), "admin"));

        Assert.Single(context.TicketAssignments);
    }

    [Fact]
    public async Task AssignTicketAsync_UnassignedHistory_AllowsNewAssignmentAndPreservesHistory()
    {
        using var context = CreateTestContext();
        context.TicketAssignments.Add(new TicketAssignment
        {
            Id = 1,
            TicketId = 1,
            AssignedToUserId = "user2",
            AssignedByUserId = "admin",
            AssignedAt = DateTime.UtcNow.AddHours(-1),
            UnassignedAt = DateTime.UtcNow.AddMinutes(-30)
        });
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByIdAsync("user2"))
            .ReturnsAsync(new IdentityUser { Id = "user2" });
        var auditService = new Mock<IAuditService>();
        auditService.Setup(x => x.LogActionAsync(
                "admin", "Ticket Assigned", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(context, userManager, auditService);
        var result = await service.AssignTicketAsync(
            CreateAssignmentDto(), "admin");

        Assert.Equal(1, result.TicketId);
        Assert.Equal(2, await context.TicketAssignments.CountAsync());
        Assert.Single(await context.TicketAssignments
            .Where(a => a.TicketId == 1 && a.UnassignedAt == null)
            .ToListAsync());
    }

    [Fact]
    public async Task GetAllAssignmentsAsync_ReturnsNewestAssignmentsFirst()
    {
        using var context = CreateTestContext();

        context.TicketAssignments.AddRange(
            new TicketAssignment
            {
                Id = 1,
                TicketId = 1,
                AssignedToUserId = "user2",
                AssignedByUserId = "admin",
                AssignedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new TicketAssignment
            {
                Id = 2,
                TicketId = 1,
                AssignedToUserId = "user3",
                AssignedByUserId = "admin",
                AssignedAt = DateTime.UtcNow
            }
        );

        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        var service = CreateService(context, userManager, auditService);

        var result = (await service.GetAllAssignmentsAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(1, result[1].Id);
    }

    [Fact]
    public async Task UnassignTicketAsync_ValidAssignment_UnassignsTicket()
    {
        using var context = CreateTestContext();

        context.TicketAssignments.Add(new TicketAssignment
        {
            Id = 1,
            TicketId = 1,
            AssignedToUserId = "user2",
            AssignedByUserId = "admin",
            AssignedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        auditService
            .Setup(x => x.LogActionAsync(
                "admin",
                "Ticket Unassigned",
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(context, userManager, auditService);

        var result = await service.UnassignTicketAsync(1, "admin");

        Assert.NotNull(result.UnassignedAt);

        auditService.Verify(
            x => x.LogActionAsync(
                "admin",
                "Ticket Unassigned",
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task UnassignTicketAsync_InvalidAssignment_ThrowsException()
    {
        using var context = CreateTestContext();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        var service = CreateService(context, userManager, auditService);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UnassignTicketAsync(99, "admin"));

        auditService.Verify(
            x => x.LogActionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task UnassignTicketAsync_AlreadyUnassigned_ThrowsException()
    {
        using var context = CreateTestContext();

        context.TicketAssignments.Add(new TicketAssignment
        {
            Id = 1,
            TicketId = 1,
            AssignedToUserId = "user2",
            AssignedByUserId = "admin",
            AssignedAt = DateTime.UtcNow.AddMinutes(-10),
            UnassignedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        var auditService = new Mock<IAuditService>();

        var service = CreateService(context, userManager, auditService);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UnassignTicketAsync(1, "admin"));
    }
}
