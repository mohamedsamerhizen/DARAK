using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase28SupportReportingManagementIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportExportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FilterJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DownloadPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportExportJobs_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportExportJobs_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FilterJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedReports_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedReports_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AssignmentNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EscalationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportCases_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportCases_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportCases_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportCases_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportCases_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportSlaPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ResponseHours = table.Column<int>(type: "int", nullable: false),
                    ResolutionHours = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportSlaPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportSlaPolicies_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportCaseEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupportCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InternalNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportCaseEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportCaseEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportCaseEvents_SupportCases_SupportCaseId",
                        column: x => x.SupportCaseId,
                        principalTable: "SupportCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_CompoundId",
                table: "ReportExportJobs",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_CompoundId_ReportType_Status",
                table: "ReportExportJobs",
                columns: new[] { "CompoundId", "ReportType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_ReportType",
                table: "ReportExportJobs",
                column: "ReportType");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_RequestedAtUtc",
                table: "ReportExportJobs",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_RequestedByUserId",
                table: "ReportExportJobs",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_Status",
                table: "ReportExportJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_CompoundId",
                table: "SavedReports",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_CompoundId_ReportType_IsActive",
                table: "SavedReports",
                columns: new[] { "CompoundId", "ReportType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_CreatedByUserId",
                table: "SavedReports",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_IsActive",
                table: "SavedReports",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SavedReports_ReportType",
                table: "SavedReports",
                column: "ReportType");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCaseEvents_ActorUserId",
                table: "SupportCaseEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCaseEvents_CreatedAtUtc",
                table: "SupportCaseEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCaseEvents_EventType",
                table: "SupportCaseEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCaseEvents_SupportCaseId",
                table: "SupportCaseEvents",
                column: "SupportCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_AssignedToUserId",
                table: "SupportCases",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_Category",
                table: "SupportCases",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_CompoundId",
                table: "SupportCases",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_CompoundId_Status_Priority_DueAtUtc",
                table: "SupportCases",
                columns: new[] { "CompoundId", "Status", "Priority", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_CreatedAtUtc",
                table: "SupportCases",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_CreatedByUserId",
                table: "SupportCases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_DueAtUtc",
                table: "SupportCases",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_Priority",
                table: "SupportCases",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_PropertyUnitId",
                table: "SupportCases",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_ResidentProfileId",
                table: "SupportCases",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_SourceType_SourceEntityId",
                table: "SupportCases",
                columns: new[] { "SourceType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportCases_Status",
                table: "SupportCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupportSlaPolicies_CompoundId",
                table: "SupportSlaPolicies",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportSlaPolicies_CompoundId_Category_Priority",
                table: "SupportSlaPolicies",
                columns: new[] { "CompoundId", "Category", "Priority" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportExportJobs");

            migrationBuilder.DropTable(
                name: "SavedReports");

            migrationBuilder.DropTable(
                name: "SupportCaseEvents");

            migrationBuilder.DropTable(
                name: "SupportSlaPolicies");

            migrationBuilder.DropTable(
                name: "SupportCases");
        }
    }
}
