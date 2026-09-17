using Microsoft.EntityFrameworkCore;
using CatalogService.Models;

namespace CatalogService.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasIndex(b => b.Isbn);
            entity.HasIndex(b => b.Genre);
        });

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Book>().HasData(
            new Book
            {
                BookId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Isbn = "978-0-13-468599-1", Title = "Clean Code", Author = "Robert C. Martin",
                Genre = "Technology", PublicationYear = 2008,
                Description = "A handbook of agile software craftsmanship",
                Publisher = "Prentice Hall", PageCount = 464, Language = "English",
                TotalCopies = 5, AvailableCopies = 2, CreatedAt = now, UpdatedAt = now
            },
            new Book
            {
                BookId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Isbn = "978-0-13-475759-9", Title = "Refactoring", Author = "Martin Fowler",
                Genre = "Technology", PublicationYear = 2018,
                Description = "Improving the design of existing code",
                Publisher = "Addison-Wesley", PageCount = 448, Language = "English",
                TotalCopies = 3, AvailableCopies = 0, CreatedAt = now, UpdatedAt = now
            },
            new Book
            {
                BookId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Isbn = "978-0-452-28423-4", Title = "1984", Author = "George Orwell",
                Genre = "Fiction", PublicationYear = 1949,
                Description = "A dystopian social science fiction novel",
                Publisher = "Secker & Warburg", PageCount = 328, Language = "English",
                TotalCopies = 4, AvailableCopies = 4, CreatedAt = now, UpdatedAt = now
            },
            new Book
            {
                BookId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Isbn = "978-0-06-231609-7", Title = "Sapiens", Author = "Yuval Noah Harari",
                Genre = "Non-Fiction", PublicationYear = 2011,
                Description = "A brief history of humankind",
                Publisher = "Harper", PageCount = 443, Language = "English",
                TotalCopies = 2, AvailableCopies = 1, CreatedAt = now, UpdatedAt = now
            },
            new Book
            {
                BookId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Isbn = "978-0-13-235088-4", Title = "Clean Architecture", Author = "Robert C. Martin",
                Genre = "Technology", PublicationYear = 2017,
                Description = "A craftsman's guide to software structure and design",
                Publisher = "Prentice Hall", PageCount = 432, Language = "English",
                TotalCopies = 3, AvailableCopies = 3, CreatedAt = now, UpdatedAt = now
            }
        );
    }
}
