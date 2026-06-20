using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase31BFinancialGovernanceModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAdjustmentId",
                table: "ViolationAppeals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAdjustmentId",
                table: "FinancialDisputes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViolationAppeals_FinancialAdjustmentId",
                table: "ViolationAppeals",
                column: "FinancialAdjustmentId",
                filter: "[FinancialAdjustmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDisputes_FinancialAdjustmentId",
                table: "FinancialDisputes",
                column: "FinancialAdjustmentId",
                filter: "[FinancialAdjustmentId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialDisputes_FinancialAdjustments_FinancialAdjustmentId",
                table: "FinancialDisputes",
                column: "FinancialAdjustmentId",
                principalTable: "FinancialAdjustments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ViolationAppeals_FinancialAdjustments_FinancialAdjustmentId",
                table: "ViolationAppeals",
                column: "FinancialAdjustmentId",
                principalTable: "FinancialAdjustments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialDisputes_FinancialAdjustments_FinancialAdjustmentId",
                table: "FinancialDisputes");

            migrationBuilder.DropForeignKey(
                name: "FK_ViolationAppeals_FinancialAdjustments_FinancialAdjustmentId",
                table: "ViolationAppeals");

            migrationBuilder.DropIndex(
                name: "IX_ViolationAppeals_FinancialAdjustmentId",
                table: "ViolationAppeals");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDisputes_FinancialAdjustmentId",
                table: "FinancialDisputes");

            migrationBuilder.DropColumn(
                name: "FinancialAdjustmentId",
                table: "ViolationAppeals");

            migrationBuilder.DropColumn(
                name: "FinancialAdjustmentId",
                table: "FinancialDisputes");
        }
    }
}
