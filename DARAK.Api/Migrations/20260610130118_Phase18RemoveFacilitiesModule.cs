using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase18RemoveFacilitiesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityReservations");

            migrationBuilder.DropTable(
                name: "Facilities");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ViolationFines",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UtilityBills",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RentInvoices",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InstallmentScheduleItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TargetId",
                table: "Payments",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_TargetId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ViolationFines");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UtilityBills");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RentInvoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InstallmentScheduleItems");

            migrationBuilder.CreateTable(
                name: "Facilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ClosesAt = table.Column<TimeOnly>(type: "time", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FacilityType = table.Column<int>(type: "int", nullable: false),
                    HourlyFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facilities_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacilityReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReservationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityReservations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityReservations_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityReservations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacilityReservations_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_CompoundId_Name",
                table: "Facilities",
                columns: new[] { "CompoundId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_FacilityType",
                table: "Facilities",
                column: "FacilityType");

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_IsActive",
                table: "Facilities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_Name",
                table: "Facilities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReservations_FacilityId",
                table: "FacilityReservations",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReservations_FacilityId_ReservationDate",
                table: "FacilityReservations",
                columns: new[] { "FacilityId", "ReservationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReservations_PaymentId",
                table: "FacilityReservations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReservations_ReservationDate",
                table: "FacilityReservations",
                column: "ReservationDate");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReservations_ResidentProfileId",
                table: "FacilityReservations",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReservations_Status",
                table: "FacilityReservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReservations_UserId",
                table: "FacilityReservations",
                column: "UserId");
        }
    }
}
