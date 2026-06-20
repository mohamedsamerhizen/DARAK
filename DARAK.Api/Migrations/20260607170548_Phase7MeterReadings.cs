using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase7MeterReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterType = table.Column<int>(type: "int", nullable: false),
                    MeterNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meters_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Meters_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    PreviousReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrentReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Consumption = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsBilled = table.Column<bool>(type: "bit", nullable: false),
                    UtilityBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UtilityBillLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReadingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeterReadings_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeterReadings_UtilityBillLines_UtilityBillLineId",
                        column: x => x.UtilityBillLineId,
                        principalTable: "UtilityBillLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeterReadings_UtilityBills_UtilityBillId",
                        column: x => x.UtilityBillId,
                        principalTable: "UtilityBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_CompoundId",
                table: "MeterReadings",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_IsBilled",
                table: "MeterReadings",
                column: "IsBilled");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_MeterId_Year_Month",
                table: "MeterReadings",
                columns: new[] { "MeterId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_PropertyUnitId",
                table: "MeterReadings",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_UtilityBillId",
                table: "MeterReadings",
                column: "UtilityBillId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_UtilityBillLineId",
                table: "MeterReadings",
                column: "UtilityBillLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_CompoundId",
                table: "Meters",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_CompoundId_MeterNumber",
                table: "Meters",
                columns: new[] { "CompoundId", "MeterNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meters_MeterType",
                table: "Meters",
                column: "MeterType");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_PropertyUnitId",
                table: "Meters",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_PropertyUnitId_MeterType",
                table: "Meters",
                columns: new[] { "PropertyUnitId", "MeterType" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "Meters");
        }
    }
}
