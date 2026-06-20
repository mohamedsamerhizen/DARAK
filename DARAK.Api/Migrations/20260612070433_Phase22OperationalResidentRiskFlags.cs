using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase22OperationalResidentRiskFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResidentRiskFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FlagType = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceEntityType = table.Column<int>(type: "int", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DismissalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequiresSupervisorReview = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextReviewAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DismissedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidentRiskFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlags_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlags_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlags_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlags_AspNetUsers_LastReviewedByUserId",
                        column: x => x.LastReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlags_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlags_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlags_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResidentRiskFlagActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentRiskFlagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: true),
                    PreviousSeverity = table.Column<int>(type: "int", nullable: true),
                    NewSeverity = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidentRiskFlagActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlagActions_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentRiskFlagActions_ResidentRiskFlags_ResidentRiskFlagId",
                        column: x => x.ResidentRiskFlagId,
                        principalTable: "ResidentRiskFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlagActions_ActionType",
                table: "ResidentRiskFlagActions",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlagActions_ActorUserId",
                table: "ResidentRiskFlagActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlagActions_CreatedAtUtc",
                table: "ResidentRiskFlagActions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlagActions_ResidentRiskFlagId",
                table: "ResidentRiskFlagActions",
                column: "ResidentRiskFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_AssignedToUserId",
                table: "ResidentRiskFlags",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_ClosedByUserId",
                table: "ResidentRiskFlags",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_CompoundId",
                table: "ResidentRiskFlags",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_CompoundId_Status_Severity",
                table: "ResidentRiskFlags",
                columns: new[] { "CompoundId", "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_CreatedAtUtc",
                table: "ResidentRiskFlags",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_CreatedByUserId",
                table: "ResidentRiskFlags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_ExpiresAtUtc",
                table: "ResidentRiskFlags",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_FlagType",
                table: "ResidentRiskFlags",
                column: "FlagType");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_LastReviewedByUserId",
                table: "ResidentRiskFlags",
                column: "LastReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_NextReviewAtUtc",
                table: "ResidentRiskFlags",
                column: "NextReviewAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_PropertyUnitId",
                table: "ResidentRiskFlags",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_RequiresSupervisorReview",
                table: "ResidentRiskFlags",
                column: "RequiresSupervisorReview");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_ResidentProfileId",
                table: "ResidentRiskFlags",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_ResidentProfileId_Status_Severity",
                table: "ResidentRiskFlags",
                columns: new[] { "ResidentProfileId", "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_Severity",
                table: "ResidentRiskFlags",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_Source",
                table: "ResidentRiskFlags",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_SourceEntityType_SourceEntityId",
                table: "ResidentRiskFlags",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResidentRiskFlags_Status",
                table: "ResidentRiskFlags",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResidentRiskFlagActions");

            migrationBuilder.DropTable(
                name: "ResidentRiskFlags");
        }
    }
}
