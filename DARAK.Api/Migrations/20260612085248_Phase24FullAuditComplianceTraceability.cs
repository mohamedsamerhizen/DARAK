using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase24FullAuditComplianceTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    BeforeValuesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    AfterValuesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogEntries_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogEntries_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditLogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogChanges_AuditLogEntries_AuditLogEntryId",
                        column: x => x.AuditLogEntryId,
                        principalTable: "AuditLogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogChanges_AuditLogEntryId",
                table: "AuditLogChanges",
                column: "AuditLogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogChanges_PropertyName",
                table: "AuditLogChanges",
                column: "PropertyName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ActionType",
                table: "AuditLogEntries",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ActorUserId",
                table: "AuditLogEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_CompoundId",
                table: "AuditLogEntries",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_CompoundId_CreatedAtUtc",
                table: "AuditLogEntries",
                columns: new[] { "CompoundId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_CreatedAtUtc",
                table: "AuditLogEntries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EntityId",
                table: "AuditLogEntries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EntityType",
                table: "AuditLogEntries",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EntityType_EntityId_CreatedAtUtc",
                table: "AuditLogEntries",
                columns: new[] { "EntityType", "EntityId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ResidentProfileId",
                table: "AuditLogEntries",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ResidentProfileId_CreatedAtUtc",
                table: "AuditLogEntries",
                columns: new[] { "ResidentProfileId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_Severity",
                table: "AuditLogEntries",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_SourceModule",
                table: "AuditLogEntries",
                column: "SourceModule");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogChanges");

            migrationBuilder.DropTable(
                name: "AuditLogEntries");
        }
    }
}
