using DARAK.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260613133000_Phase3BIndexesAndOutboxAtomicity")]
    public partial class Phase3BIndexesAndOutboxAtomicity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompoundId_PaymentStatus_CreatedAt",
                table: "Payments",
                columns: new[] { "CompoundId", "PaymentStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ResidentProfileId_PaymentStatus_CreatedAt",
                table: "Payments",
                columns: new[] { "ResidentProfileId", "PaymentStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TargetType_TargetId_PaymentStatus",
                table: "Payments",
                columns: new[] { "TargetType", "TargetId", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Provider_ProviderTransactionId",
                table: "PaymentAttempts",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_CompoundId_CreatedAtUtc",
                table: "ActivityEvents",
                columns: new[] { "CompoundId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_ResidentProfileId_CreatedAtUtc",
                table: "ActivityEvents",
                columns: new[] { "ResidentProfileId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_EntityType_EntityId_CreatedAtUtc",
                table: "ActivityEvents",
                columns: new[] { "EntityType", "EntityId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status_ScheduledAtUtc_Priority",
                table: "NotificationOutbox",
                columns: new[] { "Status", "ScheduledAtUtc", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status_NextRetryAtUtc",
                table: "NotificationOutbox",
                columns: new[] { "Status", "NextRetryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ViolationFines_CompoundId_ResidentProfileId_Status_DueDate",
                table: "ViolationFines",
                columns: new[] { "CompoundId", "ResidentProfileId", "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ViolationFines_CompoundId_ResidentProfileId_Status_DueDate",
                table: "ViolationFines");

            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_Status_NextRetryAtUtc",
                table: "NotificationOutbox");

            migrationBuilder.DropIndex(
                name: "IX_NotificationOutbox_Status_ScheduledAtUtc_Priority",
                table: "NotificationOutbox");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_EntityType_EntityId_CreatedAtUtc",
                table: "ActivityEvents");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_ResidentProfileId_CreatedAtUtc",
                table: "ActivityEvents");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_CompoundId_CreatedAtUtc",
                table: "ActivityEvents");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAttempts_Provider_ProviderTransactionId",
                table: "PaymentAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TargetType_TargetId_PaymentStatus",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ResidentProfileId_PaymentStatus_CreatedAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CompoundId_PaymentStatus_CreatedAt",
                table: "Payments");

        }
    }
}
