using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase23FinancialControlCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_AspNetUsers_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialAdjustments_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResidentLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidentLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidentLedgerEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentLedgerEntries_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentLedgerEntries_FinancialAdjustments_FinancialAdjustmentId",
                        column: x => x.FinancialAdjustmentId,
                        principalTable: "FinancialAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentLedgerEntries_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_AdjustmentType",
                table: "FinancialAdjustments",
                column: "AdjustmentType");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_AppliedByUserId",
                table: "FinancialAdjustments",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_ApprovalRequestId",
                table: "FinancialAdjustments",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_CancelledByUserId",
                table: "FinancialAdjustments",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_CompoundId",
                table: "FinancialAdjustments",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_CompoundId_Status_CreatedAtUtc",
                table: "FinancialAdjustments",
                columns: new[] { "CompoundId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_CreatedAtUtc",
                table: "FinancialAdjustments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_RequestedByUserId",
                table: "FinancialAdjustments",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_ResidentProfileId",
                table: "FinancialAdjustments",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAdjustments_Status",
                table: "FinancialAdjustments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_CompoundId",
                table: "ResidentLedgerEntries",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_CompoundId_ResidentProfileId_OccurredAtUtc",
                table: "ResidentLedgerEntries",
                columns: new[] { "CompoundId", "ResidentProfileId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_CreatedByUserId",
                table: "ResidentLedgerEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_Direction",
                table: "ResidentLedgerEntries",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_FinancialAdjustmentId",
                table: "ResidentLedgerEntries",
                column: "FinancialAdjustmentId",
                unique: true,
                filter: "[FinancialAdjustmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_OccurredAtUtc",
                table: "ResidentLedgerEntries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_ResidentProfileId",
                table: "ResidentLedgerEntries",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_SourceId",
                table: "ResidentLedgerEntries",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLedgerEntries_SourceType",
                table: "ResidentLedgerEntries",
                column: "SourceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResidentLedgerEntries");

            migrationBuilder.DropTable(
                name: "FinancialAdjustments");
        }
    }
}
