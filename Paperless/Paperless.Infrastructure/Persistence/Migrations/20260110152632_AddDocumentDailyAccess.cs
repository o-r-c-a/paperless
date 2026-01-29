using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paperless.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentDailyAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentDailyAccesses",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    AccessType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDailyAccesses", x => new { x.DocumentId, x.Date, x.AccessType });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDailyAccesses_Date",
                table: "DocumentDailyAccesses",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDailyAccesses_DocumentId_Date",
                table: "DocumentDailyAccesses",
                columns: new[] { "DocumentId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentDailyAccesses");
        }
    }
}
