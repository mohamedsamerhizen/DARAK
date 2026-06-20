using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase6InstallmentsAndRent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertySaleContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleType = table.Column<int>(type: "int", nullable: false),
                    ContractStatus = table.Column<int>(type: "int", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContractDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PropertyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DownPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    FirstInstallmentDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertySaleContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertySaleContracts_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertySaleContracts_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertySaleContracts_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContractStatus = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MonthlyRentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentContracts_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentContracts_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentContracts_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstallmentScheduleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertySaleContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentScheduleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstallmentScheduleItems_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallmentScheduleItems_PropertySaleContracts_PropertySaleContractId",
                        column: x => x.PropertySaleContractId,
                        principalTable: "PropertySaleContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallmentScheduleItems_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallmentScheduleItems_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousBalanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LateFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RentInvoiceStatus = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentInvoices_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentInvoices_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentInvoices_RentContracts_RentContractId",
                        column: x => x.RentContractId,
                        principalTable: "RentContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentInvoices_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentScheduleItems_CompoundId",
                table: "InstallmentScheduleItems",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentScheduleItems_InstallmentStatus",
                table: "InstallmentScheduleItems",
                column: "InstallmentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentScheduleItems_PropertySaleContractId",
                table: "InstallmentScheduleItems",
                column: "PropertySaleContractId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentScheduleItems_PropertySaleContractId_InstallmentNumber",
                table: "InstallmentScheduleItems",
                columns: new[] { "PropertySaleContractId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentScheduleItems_PropertyUnitId",
                table: "InstallmentScheduleItems",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentScheduleItems_ResidentProfileId",
                table: "InstallmentScheduleItems",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertySaleContracts_CompoundId",
                table: "PropertySaleContracts",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertySaleContracts_ContractNumber",
                table: "PropertySaleContracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertySaleContracts_ContractStatus",
                table: "PropertySaleContracts",
                column: "ContractStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PropertySaleContracts_PropertyUnitId",
                table: "PropertySaleContracts",
                column: "PropertyUnitId",
                unique: true,
                filter: "[ContractStatus] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PropertySaleContracts_ResidentProfileId",
                table: "PropertySaleContracts",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RentContracts_CompoundId",
                table: "RentContracts",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_RentContracts_ContractNumber",
                table: "RentContracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentContracts_ContractStatus",
                table: "RentContracts",
                column: "ContractStatus");

            migrationBuilder.CreateIndex(
                name: "IX_RentContracts_PropertyUnitId",
                table: "RentContracts",
                column: "PropertyUnitId",
                unique: true,
                filter: "[ContractStatus] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RentContracts_ResidentProfileId",
                table: "RentContracts",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_CompoundId",
                table: "RentInvoices",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_InvoiceNumber",
                table: "RentInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_PropertyUnitId",
                table: "RentInvoices",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_RentContractId_Year_Month",
                table: "RentInvoices",
                columns: new[] { "RentContractId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_RentInvoiceStatus",
                table: "RentInvoices",
                column: "RentInvoiceStatus");

            migrationBuilder.CreateIndex(
                name: "IX_RentInvoices_ResidentProfileId",
                table: "RentInvoices",
                column: "ResidentProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstallmentScheduleItems");

            migrationBuilder.DropTable(
                name: "RentInvoices");

            migrationBuilder.DropTable(
                name: "PropertySaleContracts");

            migrationBuilder.DropTable(
                name: "RentContracts");
        }
    }
}
