using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase41PaymentReconciliationReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReviewDecision",
                table: "PaymentReconciliationItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "PaymentReconciliationItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "PaymentReconciliationItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "PaymentReconciliationItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_ReviewDecision",
                table: "PaymentReconciliationItems",
                column: "ReviewDecision");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_ReviewedAtUtc",
                table: "PaymentReconciliationItems",
                column: "ReviewedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_ReviewedByUserId",
                table: "PaymentReconciliationItems",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentReconciliationItems_AspNetUsers_ReviewedByUserId",
                table: "PaymentReconciliationItems",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentReconciliationItems_AspNetUsers_ReviewedByUserId",
                table: "PaymentReconciliationItems");

            migrationBuilder.DropIndex(
                name: "IX_PaymentReconciliationItems_ReviewDecision",
                table: "PaymentReconciliationItems");

            migrationBuilder.DropIndex(
                name: "IX_PaymentReconciliationItems_ReviewedAtUtc",
                table: "PaymentReconciliationItems");

            migrationBuilder.DropIndex(
                name: "IX_PaymentReconciliationItems_ReviewedByUserId",
                table: "PaymentReconciliationItems");

            migrationBuilder.DropColumn(
                name: "ReviewDecision",
                table: "PaymentReconciliationItems");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "PaymentReconciliationItems");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "PaymentReconciliationItems");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "PaymentReconciliationItems");
        }
    }
}
