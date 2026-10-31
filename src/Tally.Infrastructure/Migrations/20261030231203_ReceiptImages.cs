using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tally.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptImages_Key",
                table: "ReceiptImages",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptImages");
        }
    }
}
