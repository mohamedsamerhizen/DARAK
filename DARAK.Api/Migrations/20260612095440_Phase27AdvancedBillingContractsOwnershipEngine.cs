using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase27AdvancedBillingContractsOwnershipEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChargeMode = table.Column<int>(type: "int", nullable: false),
                    FixedChargeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumChargeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LateFeeFlatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LateFeePercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingRules_CompoundServices_CompoundServiceId",
                        column: x => x.CompoundServiceId,
                        principalTable: "CompoundServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillingRules_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractType = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractLifecycleEvents_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractLifecycleEvents_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractLifecycleEvents_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstallmentRescheduleRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentScheduleItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertySaleContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OriginalDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentRescheduleRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstallmentRescheduleRequests_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallmentRescheduleRequests_InstallmentScheduleItems_InstallmentScheduleItemId",
                        column: x => x.InstallmentScheduleItemId,
                        principalTable: "InstallmentScheduleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallmentRescheduleRequests_PropertySaleContracts_PropertySaleContractId",
                        column: x => x.PropertySaleContractId,
                        principalTable: "PropertySaleContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallmentRescheduleRequests_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadingCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterReadingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OriginalPreviousReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OriginalCurrentReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OriginalConsumption = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CorrectedPreviousReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CorrectedCurrentReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CorrectedConsumption = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CorrectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadingCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadingCorrections_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeterReadingCorrections_MeterReadings_MeterReadingId",
                        column: x => x.MeterReadingId,
                        principalTable: "MeterReadings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeterReadingCorrections_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeterReadingCorrections_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OwnershipTransferRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentOwnerResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewOwnerResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedTransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnershipTransferRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnershipTransferRequests_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OwnershipTransferRequests_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OwnershipTransferRequests_ResidentProfiles_CurrentOwnerResidentProfileId",
                        column: x => x.CurrentOwnerResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OwnershipTransferRequests_ResidentProfiles_NewOwnerResidentProfileId",
                        column: x => x.NewOwnerResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitHandoverChecklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandoverType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitHandoverChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitHandoverChecklists_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitHandoverChecklists_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitHandoverChecklists_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillingRuleTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ToQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RatePerUnit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingRuleTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingRuleTiers_BillingRules_BillingRuleId",
                        column: x => x.BillingRuleId,
                        principalTable: "BillingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitHandoverChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitHandoverChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitHandoverChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitHandoverChecklistItems_UnitHandoverChecklists_UnitHandoverChecklistId",
                        column: x => x.UnitHandoverChecklistId,
                        principalTable: "UnitHandoverChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingRules_ChargeMode",
                table: "BillingRules",
                column: "ChargeMode");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRules_CompoundId",
                table: "BillingRules",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRules_CompoundId_Status_EffectiveFrom",
                table: "BillingRules",
                columns: new[] { "CompoundId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingRules_CompoundServiceId",
                table: "BillingRules",
                column: "CompoundServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRules_Status",
                table: "BillingRules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRuleTiers_BillingRuleId",
                table: "BillingRuleTiers",
                column: "BillingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingRuleTiers_BillingRuleId_SortOrder",
                table: "BillingRuleTiers",
                columns: new[] { "BillingRuleId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractLifecycleEvents_CompoundId",
                table: "ContractLifecycleEvents",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractLifecycleEvents_ContractType_ContractId",
                table: "ContractLifecycleEvents",
                columns: new[] { "ContractType", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractLifecycleEvents_CreatedAtUtc",
                table: "ContractLifecycleEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ContractLifecycleEvents_EffectiveDate",
                table: "ContractLifecycleEvents",
                column: "EffectiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_ContractLifecycleEvents_EventType",
                table: "ContractLifecycleEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ContractLifecycleEvents_PropertyUnitId",
                table: "ContractLifecycleEvents",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractLifecycleEvents_ResidentProfileId",
                table: "ContractLifecycleEvents",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentRescheduleRequests_CompoundId",
                table: "InstallmentRescheduleRequests",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentRescheduleRequests_CompoundId_Status_RequestedAtUtc",
                table: "InstallmentRescheduleRequests",
                columns: new[] { "CompoundId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentRescheduleRequests_InstallmentScheduleItemId",
                table: "InstallmentRescheduleRequests",
                column: "InstallmentScheduleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentRescheduleRequests_PropertySaleContractId",
                table: "InstallmentRescheduleRequests",
                column: "PropertySaleContractId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentRescheduleRequests_ResidentProfileId",
                table: "InstallmentRescheduleRequests",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentRescheduleRequests_Status",
                table: "InstallmentRescheduleRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingCorrections_CompoundId",
                table: "MeterReadingCorrections",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingCorrections_CompoundId_Status_RequestedAtUtc",
                table: "MeterReadingCorrections",
                columns: new[] { "CompoundId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingCorrections_MeterId",
                table: "MeterReadingCorrections",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingCorrections_MeterReadingId",
                table: "MeterReadingCorrections",
                column: "MeterReadingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingCorrections_PropertyUnitId",
                table: "MeterReadingCorrections",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingCorrections_Status",
                table: "MeterReadingCorrections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_CompoundId",
                table: "OwnershipTransferRequests",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_CompoundId_Status_RequestedAtUtc",
                table: "OwnershipTransferRequests",
                columns: new[] { "CompoundId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_CurrentOwnerResidentProfileId",
                table: "OwnershipTransferRequests",
                column: "CurrentOwnerResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_NewOwnerResidentProfileId",
                table: "OwnershipTransferRequests",
                column: "NewOwnerResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_PropertyUnitId",
                table: "OwnershipTransferRequests",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipTransferRequests_Status",
                table: "OwnershipTransferRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UnitHandoverChecklistItems_UnitHandoverChecklistId",
                table: "UnitHandoverChecklistItems",
                column: "UnitHandoverChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitHandoverChecklistItems_UnitHandoverChecklistId_SortOrder",
                table: "UnitHandoverChecklistItems",
                columns: new[] { "UnitHandoverChecklistId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitHandoverChecklists_CompoundId",
                table: "UnitHandoverChecklists",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitHandoverChecklists_CompoundId_Status_ScheduledDate",
                table: "UnitHandoverChecklists",
                columns: new[] { "CompoundId", "Status", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitHandoverChecklists_PropertyUnitId",
                table: "UnitHandoverChecklists",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitHandoverChecklists_ResidentProfileId",
                table: "UnitHandoverChecklists",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitHandoverChecklists_Status",
                table: "UnitHandoverChecklists",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingRuleTiers");

            migrationBuilder.DropTable(
                name: "ContractLifecycleEvents");

            migrationBuilder.DropTable(
                name: "InstallmentRescheduleRequests");

            migrationBuilder.DropTable(
                name: "MeterReadingCorrections");

            migrationBuilder.DropTable(
                name: "OwnershipTransferRequests");

            migrationBuilder.DropTable(
                name: "UnitHandoverChecklistItems");

            migrationBuilder.DropTable(
                name: "BillingRules");

            migrationBuilder.DropTable(
                name: "UnitHandoverChecklists");
        }
    }
}
