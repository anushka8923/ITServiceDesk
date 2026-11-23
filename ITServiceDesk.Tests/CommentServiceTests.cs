
using System;
using System.Linq;
using System.Threading.Tasks;
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.API.Services;
using ITServiceDesk.Core.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ITServiceDesk.Tests;

public class CommentServiceTests
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

    [Fact]
    public async Task GetCommentsByTicketIdAsync_NoComments_ReturnsEmpty()
    {
        using var context = CreateTestContext();
        var service = new CommentService(context);

        var result = await service.GetCommentsByTicketIdAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCommentsByTicketIdAsync_ReturnsCommentsInCreatedOrder()
    {
        using var context = CreateTestContext();

        context.TicketComments.AddRange(
            new TicketComment
            {
                Id = 1,
                Comment = "Second comment",
                TicketId = 1,
                UserId = "user1",
                CreatedAt = DateTime.UtcNow.AddMinutes(2)
            },
            new TicketComment
            {
                Id = 2,
                Comment = "First comment",
                TicketId = 1,
                UserId = "user2",
                CreatedAt = DateTime.UtcNow.AddMinutes(1)
            }
        );

        await context.SaveChangesAsync();

        var service = new CommentService(context);
        var result = (await service.GetCommentsByTicketIdAsync(1)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("First comment", result[0].Comment);
        Assert.Equal("Second comment", result[1].Comment);
    }

    [Fact]
    public async Task AddCommentAsync_ValidData_AddsComment()
    {
        using var context = CreateTestContext();
        var service = new CommentService(context);

        var dto = new AddCommentDto
        {
            Comment = "Please check my laptop."
        };

        var result = await service.AddCommentAsync(1, dto, "user1");

        Assert.Equal("Please check my laptop.", result.Comment);
        Assert.Equal(1, result.TicketId);
        Assert.Equal("user1", result.UserId);
        Assert.True(result.Id > 0);

        Assert.Equal(1, await context.TicketComments.CountAsync());
    }

    [Fact]
    public async Task AddCommentAsync_InvalidTicket_ThrowsException()
    {
        using var context = CreateTestContext();
        var service = new CommentService(context);

        var dto = new AddCommentDto
        {
            Comment = "This ticket does not exist."
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddCommentAsync(99, dto, "user1"));

        Assert.Equal(0, await context.TicketComments.CountAsync());
    }
}
