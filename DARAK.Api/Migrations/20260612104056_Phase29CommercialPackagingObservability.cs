using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase29CommercialPackagingObservability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackgroundJobRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    WorkerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationFailureEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    FirstOccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastOccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationFailureEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationFailureEvents_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LicenseProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicensedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LicenseKeyFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Plan = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MaxCompounds = table.Column<int>(type: "int", nullable: false),
                    MaxUnits = table.Column<int>(type: "int", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseProfiles_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PendingNotifications = table.Column<int>(type: "int", nullable: false),
                    FailedNotifications = table.Column<int>(type: "int", nullable: false),
                    OpenIntegrationFailures = table.Column<int>(type: "int", nullable: false),
                    FailedBackgroundJobs24h = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ValueType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSettings_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRuns_JobName",
                table: "BackgroundJobRuns",
                column: "JobName");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRuns_JobName_Status_StartedAtUtc",
                table: "BackgroundJobRuns",
                columns: new[] { "JobName", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRuns_StartedAtUtc",
                table: "BackgroundJobRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRuns_Status",
                table: "BackgroundJobRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailureEvents_IntegrationName",
                table: "IntegrationFailureEvents",
                column: "IntegrationName");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailureEvents_IntegrationName_OperationName_Status",
                table: "IntegrationFailureEvents",
                columns: new[] { "IntegrationName", "OperationName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailureEvents_LastOccurredAtUtc",
                table: "IntegrationFailureEvents",
                column: "LastOccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailureEvents_OperationName",
                table: "IntegrationFailureEvents",
                column: "OperationName");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailureEvents_ResolvedByUserId",
                table: "IntegrationFailureEvents",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailureEvents_Status",
                table: "IntegrationFailureEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseProfiles_ExpiresAtUtc",
                table: "LicenseProfiles",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseProfiles_Plan",
                table: "LicenseProfiles",
                column: "Plan");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseProfiles_Status",
                table: "LicenseProfiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseProfiles_UpdatedByUserId",
                table: "LicenseProfiles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemHealthSnapshots_CapturedAtUtc",
                table: "SystemHealthSnapshots",
                column: "CapturedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SystemHealthSnapshots_Status",
                table: "SystemHealthSnapshots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_CompoundId",
                table: "SystemSettings",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_CompoundId_Key",
                table: "SystemSettings",
                columns: new[] { "CompoundId", "Key" },
                unique: true,
                filter: "[CompoundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_UpdatedByUserId",
                table: "SystemSettings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundJobRuns");

            migrationBuilder.DropTable(
                name: "IntegrationFailureEvents");

            migrationBuilder.DropTable(
                name: "LicenseProfiles");

            migrationBuilder.DropTable(
                name: "SystemHealthSnapshots");

            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
