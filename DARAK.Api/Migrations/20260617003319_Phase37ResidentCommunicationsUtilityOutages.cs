using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase37ResidentCommunicationsUtilityOutages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityOutages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FloorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    AffectedScope = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EstimatedStartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstimatedEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NotifyResidents = table.Column<bool>(type: "bit", nullable: false),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    OutboxItemCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityOutages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilityOutages_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityOutages_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityOutages_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityOutages_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityOutages_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityOutages_Floors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "Floors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityOutages_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UtilityOutageUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UtilityOutageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdateType = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NewEstimatedEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityOutageUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilityOutageUpdates_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityOutageUpdates_UtilityOutages_UtilityOutageId",
                        column: x => x.UtilityOutageId,
                        principalTable: "UtilityOutages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_AnnouncementId",
                table: "UtilityOutages",
                column: "AnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_BuildingId",
                table: "UtilityOutages",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_CompoundId",
                table: "UtilityOutages",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_CreatedByUserId",
                table: "UtilityOutages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_EstimatedEndAtUtc",
                table: "UtilityOutages",
                column: "EstimatedEndAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_EstimatedStartAtUtc",
                table: "UtilityOutages",
                column: "EstimatedStartAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_FloorId",
                table: "UtilityOutages",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_PropertyUnitId",
                table: "UtilityOutages",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_ResolvedByUserId",
                table: "UtilityOutages",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_ServiceType",
                table: "UtilityOutages",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_Severity",
                table: "UtilityOutages",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutages_Status",
                table: "UtilityOutages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutageUpdates_CreatedAtUtc",
                table: "UtilityOutageUpdates",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutageUpdates_CreatedByUserId",
                table: "UtilityOutageUpdates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutageUpdates_UpdateType",
                table: "UtilityOutageUpdates",
                column: "UpdateType");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityOutageUpdates_UtilityOutageId",
                table: "UtilityOutageUpdates",
                column: "UtilityOutageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityOutageUpdates");

            migrationBuilder.DropTable(
                name: "UtilityOutages");
        }
    }
}
