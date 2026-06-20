using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase5AOwnershipTransferPropertyUnitIndexFixFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_PropertyUnitId",
                table: "OwnershipTransferRequests",
                column: "PropertyUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OwnershipTransferRequests_PropertyUnitId",
                table: "OwnershipTransferRequests");
        }
    }
}
