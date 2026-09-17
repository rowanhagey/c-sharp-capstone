using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Data;
using CatalogService.DTOs;

namespace CatalogService.Controllers;

// Internal, service-to-service only. Not part of the external API contract.
[ApiController]
[Route("api/internal/books")]
public class InternalController : ControllerBase
{
    private readonly CatalogDbContext _db;

    public InternalController(CatalogDbContext db)
    {
        _db = db;
    }

    [HttpGet("{bookId}")]
    public async Task<IActionResult> GetAvailability(Guid bookId)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.BookId == bookId);
        if (book == null)
        {
            return Ok(new InternalBookAvailability { Exists = false });
        }

        return Ok(new InternalBookAvailability
        {
            BookId = book.BookId,
            Title = book.Title,
            Author = book.Author,
            AvailableCopies = book.AvailableCopies,
            Exists = true
        });
    }

    // Delta of -1 on reserve, +1 on return/release.
    [HttpPatch("{bookId}/availability")]
    public async Task<IActionResult> AdjustAvailability(Guid bookId, [FromBody] InternalAvailabilityAdjustment adjustment)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.BookId == bookId);
        if (book == null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Book not found" });
        }

        var newCount = book.AvailableCopies + adjustment.Delta;
        if (newCount < 0 || newCount > book.TotalCopies)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_ADJUSTMENT",
                Message = $"Adjustment would set availableCopies to {newCount}, which is out of range [0, {book.TotalCopies}]"
            });
        }

        book.AvailableCopies = newCount;
        book.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new InternalBookAvailability
        {
            BookId = book.BookId,
            Title = book.Title,
            Author = book.Author,
            AvailableCopies = book.AvailableCopies,
            Exists = true
        });
    }
}
