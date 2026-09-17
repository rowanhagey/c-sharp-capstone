using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Data;
using CatalogService.DTOs;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly CatalogDbContext _db;

    public CatalogController(CatalogDbContext db)
    {
        _db = db;
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string sortBy = "title",
        [FromQuery] string sortOrder = "asc",
        [FromQuery] string? query = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? isbn = null,
        [FromQuery] bool availableOnly = false)
    {
        var books = _db.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLower();
            books = books.Where(b => b.Title.ToLower().Contains(q) || b.Author.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            books = books.Where(b => b.Genre == genre);
        }

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            books = books.Where(b => b.Isbn == isbn);
        }

        if (availableOnly)
        {
            books = books.Where(b => b.AvailableCopies > 0);
        }

        books = (sortBy.ToLower(), sortOrder.ToLower()) switch
        {
            ("author", "desc") => books.OrderByDescending(b => b.Author),
            ("author", _) => books.OrderBy(b => b.Author),
            ("publicationyear", "desc") => books.OrderByDescending(b => b.PublicationYear),
            ("publicationyear", _) => books.OrderBy(b => b.PublicationYear),
            (_, "desc") => books.OrderByDescending(b => b.Title),
            _ => books.OrderBy(b => b.Title)
        };

        var totalElements = await books.CountAsync();
        var totalPages = size > 0 ? (int)Math.Ceiling(totalElements / (double)size) : 0;

        var items = await books
            .Skip(page * size)
            .Take(size)
            .Select(b => new BookListItem
            {
                BookId = b.BookId,
                Isbn = b.Isbn,
                Title = b.Title,
                Author = b.Author,
                Genre = b.Genre,
                PublicationYear = b.PublicationYear,
                Description = b.Description,
                TotalCopies = b.TotalCopies,
                AvailableCopies = b.AvailableCopies,
                Status = b.AvailableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT"
            })
            .ToListAsync();

        return Ok(new PagedResult<BookListItem>
        {
            Content = items,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpGet("books/{bookId}")]
    public async Task<IActionResult> GetBookById(Guid bookId)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.BookId == bookId);

        if (book == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {bookId}"
            });
        }

        return Ok(new BookDetail
        {
            BookId = book.BookId,
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            PublicationYear = book.PublicationYear,
            Description = book.Description,
            Publisher = book.Publisher,
            PageCount = book.PageCount,
            Language = book.Language,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Status = book.Status,
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        });
    }
}
