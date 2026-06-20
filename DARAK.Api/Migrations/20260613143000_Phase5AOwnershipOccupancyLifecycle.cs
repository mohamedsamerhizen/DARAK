using DARAK.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260613143000_Phase5AOwnershipOccupancyLifecycle")]
    public partial class Phase5AOwnershipOccupancyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_PropertyUnitId_PendingApproval",
                table: "OwnershipTransferRequests",
                column: "PropertyUnitId",
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OwnershipTransferRequests_PropertyUnitId_PendingApproval",
                table: "OwnershipTransferRequests");
        }
    }
}
