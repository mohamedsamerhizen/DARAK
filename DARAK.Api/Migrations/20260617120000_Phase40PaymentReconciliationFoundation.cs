using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase40PaymentReconciliationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentReconciliationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StatementReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReconciliationBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliationBatches_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliationBatches_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliationBatches_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentReconciliationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentReconciliationBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ProviderAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProviderStatus = table.Column<int>(type: "int", nullable: false),
                    MatchedPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedPaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchStatus = table.Column<int>(type: "int", nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IssueReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReconciliationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliationItems_PaymentAttempts_MatchedPaymentAttemptId",
                        column: x => x.MatchedPaymentAttemptId,
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliationItems_PaymentReconciliationBatches_PaymentReconciliationBatchId",
                        column: x => x.PaymentReconciliationBatchId,
                        principalTable: "PaymentReconciliationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliationItems_Payments_MatchedPaymentId",
                        column: x => x.MatchedPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationBatches_ClosedByUserId",
                table: "PaymentReconciliationBatches",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationBatches_CompoundId",
                table: "PaymentReconciliationBatches",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationBatches_CompoundId_Provider_StatementReference",
                table: "PaymentReconciliationBatches",
                columns: new[] { "CompoundId", "Provider", "StatementReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationBatches_CreatedByUserId",
                table: "PaymentReconciliationBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationBatches_Provider",
                table: "PaymentReconciliationBatches",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationBatches_StatementDate",
                table: "PaymentReconciliationBatches",
                column: "StatementDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationBatches_Status",
                table: "PaymentReconciliationBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_MatchedPaymentAttemptId",
                table: "PaymentReconciliationItems",
                column: "MatchedPaymentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_MatchedPaymentId",
                table: "PaymentReconciliationItems",
                column: "MatchedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_MatchStatus",
                table: "PaymentReconciliationItems",
                column: "MatchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_PaymentReconciliationBatchId",
                table: "PaymentReconciliationItems",
                column: "PaymentReconciliationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationItems_PaymentReconciliationBatchId_ProviderTransactionId",
                table: "PaymentReconciliationItems",
                columns: new[] { "PaymentReconciliationBatchId", "ProviderTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReconciliationItems");

            migrationBuilder.DropTable(
                name: "PaymentReconciliationBatches");
        }
    }
}
