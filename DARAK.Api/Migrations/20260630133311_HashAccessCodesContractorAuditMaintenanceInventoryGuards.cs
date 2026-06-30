using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class HashAccessCodesContractorAuditMaintenanceInventoryGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSlaEscalatedAtUtc",
                table: "WorkOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreventiveMaintenanceOccurrenceKey",
                table: "WorkOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaEscalatedAtUtc",
                table: "WorkOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaEscalationCount",
                table: "WorkOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "LastGeneratedOccurrenceKey",
                table: "PreventiveMaintenancePlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "InventoryMovements",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [VisitorPasses]
                SET [AccessCode] = CONCAT('SHA256HEX$', CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(nvarchar(4000), UPPER(LTRIM(RTRIM([AccessCode]))))), 2))
                WHERE [AccessCode] IS NOT NULL
                  AND [AccessCode] <> ''
                  AND [AccessCode] NOT LIKE 'AC2$%'
                  AND [AccessCode] NOT LIKE 'SHA256HEX$%';
                """);

            migrationBuilder.Sql("""
                UPDATE [AccessCredentials]
                SET [CredentialCode] = CONCAT('SHA256HEX$', CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(nvarchar(4000), UPPER(LTRIM(RTRIM([CredentialCode]))))), 2))
                WHERE [CredentialCode] IS NOT NULL
                  AND [CredentialCode] <> ''
                  AND [CredentialCode] NOT LIKE 'AC2$%'
                  AND [CredentialCode] NOT LIKE 'SHA256HEX$%';
                """);

            migrationBuilder.CreateTable(
                name: "ContractorAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorWorkPermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuardUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorAccessLogs_AspNetUsers_GuardUserId",
                        column: x => x.GuardUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorAccessLogs_ContractorWorkPermits_ContractorWorkPermitId",
                        column: x => x.ContractorWorkPermitId,
                        principalTable: "ContractorWorkPermits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CompoundId_SourceType_SourceEntityId_PreventiveMaintenanceOccurrenceKey",
                table: "WorkOrders",
                columns: new[] { "CompoundId", "SourceType", "SourceEntityId", "PreventiveMaintenanceOccurrenceKey" },
                unique: true,
                filter: "[PreventiveMaintenanceOccurrenceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_SlaEscalatedAtUtc",
                table: "WorkOrders",
                column: "SlaEscalatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CompoundId_Reference",
                table: "InventoryMovements",
                columns: new[] { "CompoundId", "Reference" },
                unique: true,
                filter: "[Reference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorAccessLogs_Action",
                table: "ContractorAccessLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorAccessLogs_ContractorWorkPermitId",
                table: "ContractorAccessLogs",
                column: "ContractorWorkPermitId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorAccessLogs_CreatedAtUtc",
                table: "ContractorAccessLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorAccessLogs_GuardUserId",
                table: "ContractorAccessLogs",
                column: "GuardUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractorAccessLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CompoundId_SourceType_SourceEntityId_PreventiveMaintenanceOccurrenceKey",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_SlaEscalatedAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_CompoundId_Reference",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "LastSlaEscalatedAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PreventiveMaintenanceOccurrenceKey",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SlaEscalatedAtUtc",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SlaEscalationCount",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "LastGeneratedOccurrenceKey",
                table: "PreventiveMaintenancePlans");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "InventoryMovements");
        }
    }
}
