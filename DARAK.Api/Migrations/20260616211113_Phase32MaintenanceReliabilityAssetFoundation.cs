using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase32MaintenanceReliabilityAssetFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstRespondedAtUtc",
                table: "WorkOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceAssetId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceSlaPolicyId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionDueAtUtc",
                table: "WorkOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueAtUtc",
                table: "WorkOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaBreachReason",
                table: "WorkOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachedAtUtc",
                table: "WorkOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaStatus",
                table: "WorkOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MaintenanceAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FloorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AssetType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LocationDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    InstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarrantyExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastServiceAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextServiceDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_Floors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "Floors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSlaPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: true),
                    ResponseDueMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionDueMinutes = table.Column<int>(type: "int", nullable: false),
                    EscalationDueMinutes = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSlaPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSlaPolicies_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalChecklistTemplates_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenancePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Cadence = table.Column<int>(type: "int", nullable: false),
                    CustomIntervalDays = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignedStaffMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedVendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastGeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenancePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenancePlans_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenancePlans_MaintenanceAssets_MaintenanceAssetId",
                        column: x => x.MaintenanceAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenancePlans_ServiceVendors_AssignedVendorId",
                        column: x => x.AssignedVendorId,
                        principalTable: "ServiceVendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenancePlans_StaffMembers_AssignedStaffMemberId",
                        column: x => x.AssignedStaffMemberId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalChecklistRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalChecklistTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SummaryNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalChecklistRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalChecklistRuns_AspNetUsers_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalChecklistRuns_AspNetUsers_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalChecklistRuns_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalChecklistRuns_OperationalChecklistTemplates_OperationalChecklistTemplateId",
                        column: x => x.OperationalChecklistTemplateId,
                        principalTable: "OperationalChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalChecklistTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalChecklistTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalChecklistTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalChecklistTemplateItems_OperationalChecklistTemplates_OperationalChecklistTemplateId",
                        column: x => x.OperationalChecklistTemplateId,
                        principalTable: "OperationalChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationalChecklistRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationalChecklistRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalChecklistRunItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalChecklistRunItems_OperationalChecklistRuns_OperationalChecklistRunId",
                        column: x => x.OperationalChecklistRunId,
                        principalTable: "OperationalChecklistRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenanceAssetId",
                table: "WorkOrders",
                column: "MaintenanceAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenanceSlaPolicyId",
                table: "WorkOrders",
                column: "MaintenanceSlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ResolutionDueAtUtc",
                table: "WorkOrders",
                column: "ResolutionDueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ResponseDueAtUtc",
                table: "WorkOrders",
                column: "ResponseDueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_SlaStatus",
                table: "WorkOrders",
                column: "SlaStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_AssetType",
                table: "MaintenanceAssets",
                column: "AssetType");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_BuildingId",
                table: "MaintenanceAssets",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_CompoundId",
                table: "MaintenanceAssets",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_CompoundId_Code",
                table: "MaintenanceAssets",
                columns: new[] { "CompoundId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_FloorId",
                table: "MaintenanceAssets",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_NextServiceDueAtUtc",
                table: "MaintenanceAssets",
                column: "NextServiceDueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_PropertyUnitId",
                table: "MaintenanceAssets",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_Status",
                table: "MaintenanceAssets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSlaPolicies_CompoundId",
                table: "MaintenanceSlaPolicies",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSlaPolicies_IsActive",
                table: "MaintenanceSlaPolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSlaPolicies_Priority",
                table: "MaintenanceSlaPolicies",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSlaPolicies_SourceType",
                table: "MaintenanceSlaPolicies",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRunItems_OperationalChecklistRunId",
                table: "OperationalChecklistRunItems",
                column: "OperationalChecklistRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRunItems_Status",
                table: "OperationalChecklistRunItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRuns_CompletedByUserId",
                table: "OperationalChecklistRuns",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRuns_CompoundId",
                table: "OperationalChecklistRuns",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRuns_OperationalChecklistTemplateId",
                table: "OperationalChecklistRuns",
                column: "OperationalChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRuns_StartedAtUtc",
                table: "OperationalChecklistRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRuns_StartedByUserId",
                table: "OperationalChecklistRuns",
                column: "StartedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRuns_Status",
                table: "OperationalChecklistRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistRuns_TargetType_TargetId",
                table: "OperationalChecklistRuns",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistTemplateItems_OperationalChecklistTemplateId",
                table: "OperationalChecklistTemplateItems",
                column: "OperationalChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistTemplates_CompoundId",
                table: "OperationalChecklistTemplates",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalChecklistTemplates_IsActive",
                table: "OperationalChecklistTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenancePlans_AssignedStaffMemberId",
                table: "PreventiveMaintenancePlans",
                column: "AssignedStaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenancePlans_AssignedVendorId",
                table: "PreventiveMaintenancePlans",
                column: "AssignedVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenancePlans_CompoundId",
                table: "PreventiveMaintenancePlans",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenancePlans_IsActive",
                table: "PreventiveMaintenancePlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenancePlans_MaintenanceAssetId",
                table: "PreventiveMaintenancePlans",
                column: "MaintenanceAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenancePlans_NextDueAtUtc",
                table: "PreventiveMaintenancePlans",
                column: "NextDueAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_MaintenanceAssets_MaintenanceAssetId",
                table: "WorkOrders",
                column: "MaintenanceAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_MaintenanceSlaPolicies_MaintenanceSlaPolicyId",
                table: "WorkOrders",
                column: "MaintenanceSlaPolicyId",
                principalTable: "MaintenanceSlaPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_MaintenanceAssets_MaintenanceAssetId",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_MaintenanceSlaPolicies_MaintenanceSlaPolicyId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "MaintenanceSlaPolicies");

            migrationBuilder.DropTable(
                name: "OperationalChecklistRunItems");

            migrationBuilder.DropTable(
                name: "OperationalChecklistTemplateItems");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenancePlans");

            migrationBuilder.DropTable(
                name: "OperationalChecklistRuns");

            migrationBuilder.DropTable(
                name: "MaintenanceAssets");

            migrationBuilder.DropTable(
                name: "OperationalChecklistTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_MaintenanceAssetId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_MaintenanceSlaPolicyId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_ResolutionDueAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_ResponseDueAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_SlaStatus",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "FirstRespondedAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "MaintenanceAssetId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "MaintenanceSlaPolicyId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ResolutionDueAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ResponseDueAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SlaBreachReason",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "WorkOrders");
        }
    }
}
