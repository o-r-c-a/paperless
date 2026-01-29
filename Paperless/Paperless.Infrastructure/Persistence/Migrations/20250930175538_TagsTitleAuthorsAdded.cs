using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paperless.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TagsTitleAuthorsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Authors",
                table: "Documents",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                table: "Documents",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Authors",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Documents");
        }
    }
}
