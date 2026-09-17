using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CatalogService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Isbn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Author = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Genre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PublicationYear = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Publisher = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalCopies = table.Column<int>(type: "integer", nullable: false),
                    AvailableCopies = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.BookId);
                });

            migrationBuilder.InsertData(
                table: "Books",
                columns: new[] { "BookId", "Author", "AvailableCopies", "CreatedAt", "Description", "Genre", "Isbn", "Language", "PageCount", "PublicationYear", "Publisher", "Title", "TotalCopies", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Robert C. Martin", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A handbook of agile software craftsmanship", "Technology", "978-0-13-468599-1", "English", 464, 2008, "Prentice Hall", "Clean Code", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Martin Fowler", 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Improving the design of existing code", "Technology", "978-0-13-475759-9", "English", 448, 2018, "Addison-Wesley", "Refactoring", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "George Orwell", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A dystopian social science fiction novel", "Fiction", "978-0-452-28423-4", "English", 328, 1949, "Secker & Warburg", "1984", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "Yuval Noah Harari", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A brief history of humankind", "Non-Fiction", "978-0-06-231609-7", "English", 443, 2011, "Harper", "Sapiens", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "Robert C. Martin", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "A craftsman's guide to software structure and design", "Technology", "978-0-13-235088-4", "English", 432, 2017, "Prentice Hall", "Clean Architecture", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_Genre",
                table: "Books",
                column: "Genre");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Isbn",
                table: "Books",
                column: "Isbn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Books");
        }
    }
}
