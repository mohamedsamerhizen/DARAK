using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase11FacilitiesReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Facilities",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Facilities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Facilities",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "Facilities",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "FacilityReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReservationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityReservations");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_FacilityType",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_IsActive",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_Name",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "Facilities");
        }
    }
}
