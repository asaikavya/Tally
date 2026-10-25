using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tally.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PricePlanOfferingLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PricePlans_OfferingId",
                table: "PricePlans",
                column: "OfferingId");

            migrationBuilder.AddForeignKey(
                name: "FK_PricePlans_Offerings_OfferingId",
                table: "PricePlans",
                column: "OfferingId",
                principalTable: "Offerings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PricePlans_Offerings_OfferingId",
                table: "PricePlans");

            migrationBuilder.DropIndex(
                name: "IX_PricePlans_OfferingId",
                table: "PricePlans");
        }
    }
}
