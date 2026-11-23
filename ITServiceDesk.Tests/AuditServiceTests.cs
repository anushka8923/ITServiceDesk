
using System;
using System.Threading.Tasks;
using ITServiceDesk.API.Data;
using ITServiceDesk.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ITServiceDesk.Tests;

public class AuditServiceTests
{
    private ApplicationDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task LogActionAsync_ValidData_SavesAuditLog()
    {
        using var context = CreateTestContext();
        var service = new AuditService(context);

        await service.LogActionAsync(
            "user1",
            "Ticket Created",
            "Ticket ID 1 was created."
        );

        var result = await context.AuditLogs.FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal("user1", result.UserId);
        Assert.Equal("Ticket Created", result.Action);
        Assert.Equal("Ticket ID 1 was created.", result.Details);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task LogActionAsync_SetsCreatedAtTimestamp()
    {
        using var context = CreateTestContext();
        var service = new AuditService(context);

        var before = DateTime.UtcNow;

        await service.LogActionAsync(
            "admin",
            "Ticket Updated",
            "Ticket ID 1 was updated."
        );

        var after = DateTime.UtcNow;

        var result = await context.AuditLogs.FirstAsync();

        Assert.InRange(result.CreatedAt, before, after);
    }

    [Fact]
    public async Task LogActionAsync_MultipleActions_SavesAllLogs()
    {
        using var context = CreateTestContext();
        var service = new AuditService(context);

        await service.LogActionAsync(
            "user1", "Ticket Created", "Ticket created.");

        await service.LogActionAsync(
            "admin", "Ticket Updated", "Ticket updated.");

        await service.LogActionAsync(
            "admin", "Ticket Deleted", "Ticket deleted.");

        Assert.Equal(3, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task LogActionAsync_DifferentUsers_SavesCorrectUserIds()
    {
        using var context = CreateTestContext();
        var service = new AuditService(context);

        await service.LogActionAsync(
            "user1", "Ticket Created", "Created by user1.");

        await service.LogActionAsync(
            "admin", "Ticket Assigned", "Assigned by admin.");

        var logs = await context.AuditLogs.ToListAsync();

        Assert.Equal("user1", logs[0].UserId);
        Assert.Equal("admin", logs[1].UserId);
    }
}
