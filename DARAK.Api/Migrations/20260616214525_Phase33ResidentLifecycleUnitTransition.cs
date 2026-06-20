using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase33ResidentLifecycleUnitTransition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResidentCustodyItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplacementFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReturnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidentCustodyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidentCustodyItems_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentCustodyItems_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentCustodyItems_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResidentLifecycleProcesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FinancialClearanceRequired = table.Column<bool>(type: "bit", nullable: false),
                    FinancialClearanceConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    FinancialClearanceConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinancialClearanceConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinancialClearanceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidentLifecycleProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidentLifecycleProcesses_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentLifecycleProcesses_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResidentLifecycleProcesses_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MoveLogisticsPermits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentLifecycleProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MoveType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledStartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TruckInfo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WorkersCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoveLogisticsPermits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoveLogisticsPermits_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MoveLogisticsPermits_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MoveLogisticsPermits_ResidentLifecycleProcesses_ResidentLifecycleProcessId",
                        column: x => x.ResidentLifecycleProcessId,
                        principalTable: "ResidentLifecycleProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MoveLogisticsPermits_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitDamageLiabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentLifecycleProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FinancialAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitDamageLiabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitDamageLiabilities_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitDamageLiabilities_FinancialAdjustments_FinancialAdjustmentId",
                        column: x => x.FinancialAdjustmentId,
                        principalTable: "FinancialAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitDamageLiabilities_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitDamageLiabilities_ResidentLifecycleProcesses_ResidentLifecycleProcessId",
                        column: x => x.ResidentLifecycleProcessId,
                        principalTable: "ResidentLifecycleProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitDamageLiabilities_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitDamageLiabilities_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitReadinessRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentLifecycleProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OperationalChecklistRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitReadinessRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitReadinessRecords_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitReadinessRecords_OperationalChecklistRuns_OperationalChecklistRunId",
                        column: x => x.OperationalChecklistRunId,
                        principalTable: "OperationalChecklistRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitReadinessRecords_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitReadinessRecords_ResidentLifecycleProcesses_ResidentLifecycleProcessId",
                        column: x => x.ResidentLifecycleProcessId,
                        principalTable: "ResidentLifecycleProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoveLogisticsPermits_CompoundId",
                table: "MoveLogisticsPermits",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_MoveLogisticsPermits_CompoundId_Status_ScheduledStartAtUtc",
                table: "MoveLogisticsPermits",
                columns: new[] { "CompoundId", "Status", "ScheduledStartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MoveLogisticsPermits_PropertyUnitId",
                table: "MoveLogisticsPermits",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MoveLogisticsPermits_ResidentLifecycleProcessId",
                table: "MoveLogisticsPermits",
                column: "ResidentLifecycleProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_MoveLogisticsPermits_ResidentProfileId",
                table: "MoveLogisticsPermits",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MoveLogisticsPermits_Status",
                table: "MoveLogisticsPermits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentCustodyItems_CompoundId",
                table: "ResidentCustodyItems",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentCustodyItems_CompoundId_Identifier_Status",
                table: "ResidentCustodyItems",
                columns: new[] { "CompoundId", "Identifier", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ResidentCustodyItems_PropertyUnitId",
                table: "ResidentCustodyItems",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentCustodyItems_ResidentProfileId",
                table: "ResidentCustodyItems",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentCustodyItems_Status",
                table: "ResidentCustodyItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLifecycleProcesses_CompoundId",
                table: "ResidentLifecycleProcesses",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLifecycleProcesses_CompoundId_Status_TargetDate",
                table: "ResidentLifecycleProcesses",
                columns: new[] { "CompoundId", "Status", "TargetDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLifecycleProcesses_PropertyUnitId",
                table: "ResidentLifecycleProcesses",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLifecycleProcesses_ResidentProfileId",
                table: "ResidentLifecycleProcesses",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentLifecycleProcesses_Status",
                table: "ResidentLifecycleProcesses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDamageLiabilities_CompoundId",
                table: "UnitDamageLiabilities",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDamageLiabilities_FinancialAdjustmentId",
                table: "UnitDamageLiabilities",
                column: "FinancialAdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDamageLiabilities_PropertyUnitId",
                table: "UnitDamageLiabilities",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDamageLiabilities_ResidentLifecycleProcessId",
                table: "UnitDamageLiabilities",
                column: "ResidentLifecycleProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDamageLiabilities_ResidentProfileId",
                table: "UnitDamageLiabilities",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDamageLiabilities_Status",
                table: "UnitDamageLiabilities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDamageLiabilities_WorkOrderId",
                table: "UnitDamageLiabilities",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitReadinessRecords_CompoundId",
                table: "UnitReadinessRecords",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitReadinessRecords_CompoundId_Status",
                table: "UnitReadinessRecords",
                columns: new[] { "CompoundId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitReadinessRecords_OperationalChecklistRunId",
                table: "UnitReadinessRecords",
                column: "OperationalChecklistRunId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitReadinessRecords_PropertyUnitId",
                table: "UnitReadinessRecords",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitReadinessRecords_ResidentLifecycleProcessId",
                table: "UnitReadinessRecords",
                column: "ResidentLifecycleProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitReadinessRecords_Status",
                table: "UnitReadinessRecords",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoveLogisticsPermits");

            migrationBuilder.DropTable(
                name: "ResidentCustodyItems");

            migrationBuilder.DropTable(
                name: "UnitDamageLiabilities");

            migrationBuilder.DropTable(
                name: "UnitReadinessRecords");

            migrationBuilder.DropTable(
                name: "ResidentLifecycleProcesses");
        }
    }
}
