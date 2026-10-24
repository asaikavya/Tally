using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tally.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePayUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayUrl",
                table: "Invoices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayUrl",
                table: "Invoices");
        }
    }
}
