using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tally.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PayTokenAndParentSaysPaid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayToken",
                table: "Payers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ParentSaysPaidAtUtc",
                table: "Charges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentSaysPaidNote",
                table: "Charges",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            // Give every existing payer their own random token (two GUIDs = 64 hex chars).
            migrationBuilder.Sql(
                "UPDATE Payers SET PayToken = LOWER(REPLACE(CONVERT(varchar(36), NEWID()), '-', '') + REPLACE(CONVERT(varchar(36), NEWID()), '-', '')) WHERE PayToken = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Payers_PayToken",
                table: "Payers",
                column: "PayToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payers_PayToken",
                table: "Payers");

            migrationBuilder.DropColumn(
                name: "PayToken",
                table: "Payers");

            migrationBuilder.DropColumn(
                name: "ParentSaysPaidAtUtc",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "ParentSaysPaidNote",
                table: "Charges");
        }
    }
}
