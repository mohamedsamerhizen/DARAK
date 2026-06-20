using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase39GovernanceArbitration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArbitrationCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FinalDecision = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FinalDecisionSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionIssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArbitrationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArbitrationCases_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArbitrationCases_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArbitrationCases_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArbitrationCases_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArbitrationCases_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArbitrationCaseEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArbitrationCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArbitrationCaseEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArbitrationCaseEvents_ArbitrationCases_ArbitrationCaseId",
                        column: x => x.ArbitrationCaseId,
                        principalTable: "ArbitrationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArbitrationCaseEvents_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCaseEvents_ArbitrationCaseId",
                table: "ArbitrationCaseEvents",
                column: "ArbitrationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCaseEvents_CreatedAtUtc",
                table: "ArbitrationCaseEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCaseEvents_CreatedByUserId",
                table: "ArbitrationCaseEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCaseEvents_EventType",
                table: "ArbitrationCaseEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_CancelledByUserId",
                table: "ArbitrationCases",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_CompoundId",
                table: "ArbitrationCases",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_CompoundId_Status_CreatedAtUtc",
                table: "ArbitrationCases",
                columns: new[] { "CompoundId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_CreatedByUserId",
                table: "ArbitrationCases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_DecidedByUserId",
                table: "ArbitrationCases",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_Priority",
                table: "ArbitrationCases",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_ResidentProfileId",
                table: "ArbitrationCases",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_SourceType_SourceId",
                table: "ArbitrationCases",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArbitrationCases_Status",
                table: "ArbitrationCases",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArbitrationCaseEvents");

            migrationBuilder.DropTable(
                name: "ArbitrationCases");
        }
    }
}
