using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase30FinancialGovernanceDisputesAppeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialDisputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResidentMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AdminDecisionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialDisputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialDisputes_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialDisputes_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialDisputes_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialDisputes_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialDisputes_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialDisputes_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialDisputes_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ViolationAppeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViolationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViolationFineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResidentMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AdminDecisionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReducedFineAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViolationAppeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViolationAppeals_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViolationAppeals_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViolationAppeals_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViolationAppeals_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViolationAppeals_ViolationFines_ViolationFineId",
                        column: x => x.ViolationFineId,
                        principalTable: "ViolationFines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ViolationAppeals_Violations_ViolationId",
                        column: x => x.ViolationId,
                        principalTable: "Violations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_CancelledByUserId",
                table: "FinancialDisputes",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_CompoundId",
                table: "FinancialDisputes",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_CompoundId_ResidentProfileId_TargetType_TargetId_Status",
                table: "FinancialDisputes",
                columns: new[] { "CompoundId", "ResidentProfileId", "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_CompoundId_Status_CreatedAtUtc",
                table: "FinancialDisputes",
                columns: new[] { "CompoundId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_ConversationId",
                table: "FinancialDisputes",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_CreatedByUserId",
                table: "FinancialDisputes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_ResidentProfileId",
                table: "FinancialDisputes",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_ResolvedByUserId",
                table: "FinancialDisputes",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_ReviewedByUserId",
                table: "FinancialDisputes",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_Status",
                table: "FinancialDisputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_TargetType_TargetId",
                table: "FinancialDisputes",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_CompoundId",
                table: "ViolationAppeals",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_CompoundId_ResidentProfileId_ViolationId_ViolationFineId_Status",
                table: "ViolationAppeals",
                columns: new[] { "CompoundId", "ResidentProfileId", "ViolationId", "ViolationFineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_CompoundId_Status_CreatedAtUtc",
                table: "ViolationAppeals",
                columns: new[] { "CompoundId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_CreatedByUserId",
                table: "ViolationAppeals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_ResidentProfileId",
                table: "ViolationAppeals",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_ReviewedByUserId",
                table: "ViolationAppeals",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_Status",
                table: "ViolationAppeals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_ViolationFineId",
                table: "ViolationAppeals",
                column: "ViolationFineId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_ViolationId",
                table: "ViolationAppeals",
                column: "ViolationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialDisputes");

            migrationBuilder.DropTable(
                name: "ViolationAppeals");
        }
    }
}
