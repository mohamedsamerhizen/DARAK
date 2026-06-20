using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase38SmartMeterIoTReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmartMeterDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    HealthStatus = table.Column<int>(type: "int", nullable: false),
                    ExpectedReadIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    OfflineAfterMinutes = table.Column<int>(type: "int", nullable: false),
                    SuspiciousConsumptionThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReadingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReadingValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartMeterDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartMeterDevices_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmartMeterDevices_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmartMeterReadingIngestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SmartMeterDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterReadingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    PreviousReading = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentReading = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Consumption = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AnomalyType = table.Column<int>(type: "int", nullable: false),
                    BillingHoldRecommended = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReadingTimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartMeterReadingIngestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartMeterReadingIngestions_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmartMeterReadingIngestions_MeterReadings_MeterReadingId",
                        column: x => x.MeterReadingId,
                        principalTable: "MeterReadings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmartMeterReadingIngestions_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmartMeterReadingIngestions_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmartMeterReadingIngestions_SmartMeterDevices_SmartMeterDeviceId",
                        column: x => x.SmartMeterDeviceId,
                        principalTable: "SmartMeterDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterDevices_CompoundId_DeviceIdentifier",
                table: "SmartMeterDevices",
                columns: new[] { "CompoundId", "DeviceIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterDevices_HealthStatus",
                table: "SmartMeterDevices",
                column: "HealthStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterDevices_LastSeenAtUtc",
                table: "SmartMeterDevices",
                column: "LastSeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterDevices_MeterId",
                table: "SmartMeterDevices",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterDevices_Status",
                table: "SmartMeterDevices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_AnomalyType",
                table: "SmartMeterReadingIngestions",
                column: "AnomalyType");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_BillingHoldRecommended",
                table: "SmartMeterReadingIngestions",
                column: "BillingHoldRecommended");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_CompoundId",
                table: "SmartMeterReadingIngestions",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_MeterId",
                table: "SmartMeterReadingIngestions",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_MeterId_Year_Month",
                table: "SmartMeterReadingIngestions",
                columns: new[] { "MeterId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_MeterReadingId",
                table: "SmartMeterReadingIngestions",
                column: "MeterReadingId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_PropertyUnitId",
                table: "SmartMeterReadingIngestions",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_SmartMeterDeviceId",
                table: "SmartMeterReadingIngestions",
                column: "SmartMeterDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartMeterReadingIngestions_Status",
                table: "SmartMeterReadingIngestions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmartMeterReadingIngestions");

            migrationBuilder.DropTable(
                name: "SmartMeterDevices");
        }
    }
}
