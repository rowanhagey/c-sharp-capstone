using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Controllers;
using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.Models;
using Xunit;

namespace CatalogService.Tests.Controllers;

public class InternalControllerTests
{
    private static CatalogDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CatalogDbContext(options);
    }

    private static Book AddBook(CatalogDbContext db, int total, int available)
    {
        var book = new Book
        {
            BookId = Guid.NewGuid(),
            Isbn = "111",
            Title = "Test Book",
            Author = "Test Author",
            TotalCopies = total,
            AvailableCopies = available
        };
        db.Books.Add(book);
        db.SaveChanges();
        return book;
    }

    [Fact]
    public async Task GetAvailability_ExistingBook_ReturnsCorrectCounts()
    {
        var db = CreateInMemoryDb();
        var book = AddBook(db, total: 5, available: 3);
        var controller = new InternalController(db);

        var result = await controller.GetAvailability(book.BookId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InternalBookAvailability>(okResult.Value);
        Assert.True(response.Exists);
        Assert.Equal(3, response.AvailableCopies);
    }

    [Fact]
    public async Task GetAvailability_NonExistentBook_ReturnsExistsFalse()
    {
        var db = CreateInMemoryDb();
        var controller = new InternalController(db);

        var result = await controller.GetAvailability(Guid.NewGuid());

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InternalBookAvailability>(okResult.Value);
        Assert.False(response.Exists);
    }

    [Fact]
    public async Task AdjustAvailability_DecrementWithinRange_Succeeds()
    {
        var db = CreateInMemoryDb();
        var book = AddBook(db, total: 5, available: 3);
        var controller = new InternalController(db);

        var result = await controller.AdjustAvailability(book.BookId, new InternalAvailabilityAdjustment { Delta = -1 });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InternalBookAvailability>(okResult.Value);
        Assert.Equal(2, response.AvailableCopies);
    }

    [Fact]
    public async Task AdjustAvailability_IncrementWithinRange_Succeeds()
    {
        var db = CreateInMemoryDb();
        var book = AddBook(db, total: 5, available: 3);
        var controller = new InternalController(db);

        var result = await controller.AdjustAvailability(book.BookId, new InternalAvailabilityAdjustment { Delta = 1 });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InternalBookAvailability>(okResult.Value);
        Assert.Equal(4, response.AvailableCopies);
    }

    [Fact]
    public async Task AdjustAvailability_WouldGoBelowZero_ReturnsBadRequest()
    {
        var db = CreateInMemoryDb();
        var book = AddBook(db, total: 5, available: 0);
        var controller = new InternalController(db);

        var result = await controller.AdjustAvailability(book.BookId, new InternalAvailabilityAdjustment { Delta = -1 });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_ADJUSTMENT", error.Error);
    }

    [Fact]
    public async Task AdjustAvailability_WouldExceedTotalCopies_ReturnsBadRequest()
    {
        var db = CreateInMemoryDb();
        var book = AddBook(db, total: 5, available: 5);
        var controller = new InternalController(db);

        var result = await controller.AdjustAvailability(book.BookId, new InternalAvailabilityAdjustment { Delta = 1 });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_ADJUSTMENT", error.Error);
    }

    [Fact]
    public async Task AdjustAvailability_NonExistentBook_Returns404()
    {
        var db = CreateInMemoryDb();
        var controller = new InternalController(db);

        var result = await controller.AdjustAvailability(Guid.NewGuid(), new InternalAvailabilityAdjustment { Delta = 1 });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
