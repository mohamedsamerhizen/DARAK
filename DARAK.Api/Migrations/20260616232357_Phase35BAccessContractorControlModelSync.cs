using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase35BAccessContractorControlModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractorWorkPermits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelatedWorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WorkArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EquipmentList = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RiskLevel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AllowedFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllowedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiresEscort = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeniedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeniedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DenialReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GuardCheckedInByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GuardCheckedOutByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedOutAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GuardNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorWorkPermits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorWorkPermits_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorWorkPermits_ServiceVendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "ServiceVendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorWorkPermits_WorkOrders_RelatedWorkOrderId",
                        column: x => x.RelatedWorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OwnerType = table.Column<int>(type: "int", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerDisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CredentialCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceVisitorPassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceContractorWorkPermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCredentials_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessCredentials_ContractorWorkPermits_SourceContractorWorkPermitId",
                        column: x => x.SourceContractorWorkPermitId,
                        principalTable: "ContractorWorkPermits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessCredentials_VisitorPasses_SourceVisitorPassId",
                        column: x => x.SourceVisitorPassId,
                        principalTable: "VisitorPasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_CompoundId",
                table: "AccessCredentials",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_CredentialCode",
                table: "AccessCredentials",
                column: "CredentialCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_CredentialType",
                table: "AccessCredentials",
                column: "CredentialType");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_OwnerEntityId",
                table: "AccessCredentials",
                column: "OwnerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_OwnerType",
                table: "AccessCredentials",
                column: "OwnerType");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_SourceContractorWorkPermitId",
                table: "AccessCredentials",
                column: "SourceContractorWorkPermitId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_SourceVisitorPassId",
                table: "AccessCredentials",
                column: "SourceVisitorPassId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCredentials_Status",
                table: "AccessCredentials",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkPermits_AllowedFromUtc_AllowedUntilUtc",
                table: "ContractorWorkPermits",
                columns: new[] { "AllowedFromUtc", "AllowedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkPermits_CompoundId",
                table: "ContractorWorkPermits",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkPermits_RelatedWorkOrderId",
                table: "ContractorWorkPermits",
                column: "RelatedWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkPermits_RiskLevel",
                table: "ContractorWorkPermits",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkPermits_Status",
                table: "ContractorWorkPermits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkPermits_VendorId",
                table: "ContractorWorkPermits",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessCredentials");

            migrationBuilder.DropTable(
                name: "ContractorWorkPermits");
        }
    }
}
