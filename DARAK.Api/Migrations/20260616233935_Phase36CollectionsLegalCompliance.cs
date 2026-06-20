using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase36CollectionsLegalCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionCases_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionCases_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionCases_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionCases_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionCases_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PenaltyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PercentageRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PauseWhenDisputed = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PenaltyRules_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PenaltyRules_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalNotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NoticeType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DeliveryChannel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DeliveryReference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    DeadlineDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalNotices_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalNotices_AspNetUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalNotices_CollectionCases_CollectionCaseId",
                        column: x => x.CollectionCaseId,
                        principalTable: "CollectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalNotices_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalNotices_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_CollectionCases_CollectionCaseId",
                        column: x => x.CollectionCaseId,
                        principalTable: "CollectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlanInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlanInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlanInstallments_PaymentPlans_PaymentPlanId",
                        column: x => x.PaymentPlanId,
                        principalTable: "PaymentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_AssignedToUserId",
                table: "CollectionCases",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_ClosedByUserId",
                table: "CollectionCases",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_CompoundId",
                table: "CollectionCases",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_CompoundId_Status_Stage",
                table: "CollectionCases",
                columns: new[] { "CompoundId", "Status", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_CreatedByUserId",
                table: "CollectionCases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_ResidentProfileId",
                table: "CollectionCases",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_SourceType_SourceId",
                table: "CollectionCases",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_Stage",
                table: "CollectionCases",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_Status",
                table: "CollectionCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_CollectionCaseId",
                table: "LegalNotices",
                column: "CollectionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_CompoundId",
                table: "LegalNotices",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_CompoundId_Status_CreatedAtUtc",
                table: "LegalNotices",
                columns: new[] { "CompoundId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_CreatedByUserId",
                table: "LegalNotices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_IssuedByUserId",
                table: "LegalNotices",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_NoticeType",
                table: "LegalNotices",
                column: "NoticeType");

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_ResidentProfileId",
                table: "LegalNotices",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalNotices_Status",
                table: "LegalNotices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_DueDate",
                table: "PaymentPlanInstallments",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_PaymentPlanId",
                table: "PaymentPlanInstallments",
                column: "PaymentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanInstallments_Status",
                table: "PaymentPlanInstallments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_CollectionCaseId",
                table: "PaymentPlans",
                column: "CollectionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_CompoundId",
                table: "PaymentPlans",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_CreatedByUserId",
                table: "PaymentPlans",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_ResidentProfileId",
                table: "PaymentPlans",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_Status",
                table: "PaymentPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyRules_CompoundId",
                table: "PenaltyRules",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyRules_CompoundId_Name_TargetType",
                table: "PenaltyRules",
                columns: new[] { "CompoundId", "Name", "TargetType" });

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyRules_CreatedByUserId",
                table: "PenaltyRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyRules_Status",
                table: "PenaltyRules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyRules_TargetType",
                table: "PenaltyRules",
                column: "TargetType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalNotices");

            migrationBuilder.DropTable(
                name: "PaymentPlanInstallments");

            migrationBuilder.DropTable(
                name: "PenaltyRules");

            migrationBuilder.DropTable(
                name: "PaymentPlans");

            migrationBuilder.DropTable(
                name: "CollectionCases");
        }
    }
}
