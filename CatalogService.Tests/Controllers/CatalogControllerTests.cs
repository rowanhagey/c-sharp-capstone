using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Controllers;
using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.Models;
using Xunit;

namespace CatalogService.Tests.Controllers;

public class CatalogControllerTests
{
    private static CatalogDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CatalogDbContext(options);

        db.Books.AddRange(
            new Book { BookId = Guid.NewGuid(), Isbn = "111", Title = "Clean Code", Author = "Robert C. Martin", Genre = "Technology", PublicationYear = 2008, TotalCopies = 5, AvailableCopies = 2 },
            new Book { BookId = Guid.NewGuid(), Isbn = "222", Title = "Refactoring", Author = "Martin Fowler", Genre = "Technology", PublicationYear = 2018, TotalCopies = 3, AvailableCopies = 0 },
            new Book { BookId = Guid.NewGuid(), Isbn = "333", Title = "1984", Author = "George Orwell", Genre = "Fiction", PublicationYear = 1949, TotalCopies = 4, AvailableCopies = 4 }
        );
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task GetBooks_NoFilters_ReturnsAllBooksWithPaginationMetadata()
    {
        var db = CreateInMemoryDb();
        var controller = new CatalogController(db);

        var result = await controller.GetBooks();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResult<BookListItem>>(okResult.Value);
        Assert.Equal(3, response.TotalElements);
        Assert.Equal(0, response.Page);
        Assert.True(response.Last);
    }

    [Fact]
    public async Task GetBooks_WithQuery_FiltersOnTitleAndAuthor()
    {
        var db = CreateInMemoryDb();
        var controller = new CatalogController(db);

        var result = await controller.GetBooks(query: "clean");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResult<BookListItem>>(okResult.Value);
        Assert.Single(response.Content);
        Assert.Equal("Clean Code", response.Content[0].Title);
    }

    [Fact]
    public async Task GetBooks_WithGenreFilter_ReturnsOnlyMatchingGenre()
    {
        var db = CreateInMemoryDb();
        var controller = new CatalogController(db);

        var result = await controller.GetBooks(genre: "Fiction");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResult<BookListItem>>(okResult.Value);
        Assert.Single(response.Content);
        Assert.Equal("1984", response.Content[0].Title);
    }

    [Fact]
    public async Task GetBooks_WithAvailableOnly_ExcludesZeroCopyBooks()
    {
        var db = CreateInMemoryDb();
        var controller = new CatalogController(db);

        var result = await controller.GetBooks(availableOnly: true);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResult<BookListItem>>(okResult.Value);
        Assert.DoesNotContain(response.Content, b => b.Title == "Refactoring");
    }

    [Fact]
    public async Task GetBooks_SortByPublicationYearDesc_OrdersCorrectly()
    {
        var db = CreateInMemoryDb();
        var controller = new CatalogController(db);

        var result = await controller.GetBooks(sortBy: "publicationYear", sortOrder: "desc");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResult<BookListItem>>(okResult.Value);
        Assert.Equal("Refactoring", response.Content[0].Title); // 2018 is most recent
    }

    [Fact]
    public async Task GetBooks_Pagination_ReturnsCorrectPageSize()
    {
        var db = CreateInMemoryDb();
        var controller = new CatalogController(db);

        var result = await controller.GetBooks(page: 0, size: 2);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResult<BookListItem>>(okResult.Value);
        Assert.Equal(2, response.Content.Count);
        Assert.Equal(2, response.TotalPages);
        Assert.False(response.Last);
    }

    [Fact]
    public async Task GetBookById_ExistingBook_ReturnsFullDetail()
    {
        var db = CreateInMemoryDb();
        var book = db.Books.First(b => b.Title == "Clean Code");
        var controller = new CatalogController(db);

        var result = await controller.GetBookById(book.BookId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<BookDetail>(okResult.Value);
        Assert.Equal("Clean Code", detail.Title);
        Assert.Equal("AVAILABLE", detail.Status);
    }

    [Fact]
    public async Task GetBookById_NonExistentBook_Returns404()
    {
        var db = CreateInMemoryDb();
        var controller = new CatalogController(db);

        var result = await controller.GetBookById(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFound.Value);
        Assert.Equal("NOT_FOUND", error.Error);
    }
}
