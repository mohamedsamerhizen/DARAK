using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompoundScopeToStaffAndVendors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompoundId",
                table: "StaffMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompoundId",
                table: "ServiceVendors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Compounds])
                BEGIN
                    DECLARE @FallbackCompoundId uniqueidentifier;
                    SELECT TOP(1) @FallbackCompoundId = [Id] FROM [Compounds] ORDER BY [CreatedAt], [Id];

                    UPDATE [StaffMembers]
                    SET [CompoundId] = @FallbackCompoundId
                    WHERE [CompoundId] IS NULL;

                    UPDATE [ServiceVendors]
                    SET [CompoundId] = @FallbackCompoundId
                    WHERE [CompoundId] IS NULL;
                END

                IF EXISTS (SELECT 1 FROM [StaffMembers] WHERE [CompoundId] IS NULL)
                    THROW 51000, 'Cannot backfill StaffMembers.CompoundId because no compounds exist.', 1;

                IF EXISTS (SELECT 1 FROM [ServiceVendors] WHERE [CompoundId] IS NULL)
                    THROW 51001, 'Cannot backfill ServiceVendors.CompoundId because no compounds exist.', 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompoundId",
                table: "StaffMembers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompoundId",
                table: "ServiceVendors",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_CompoundId",
                table: "StaffMembers",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVendors_CompoundId",
                table: "ServiceVendors",
                column: "CompoundId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceVendors_Compounds_CompoundId",
                table: "ServiceVendors",
                column: "CompoundId",
                principalTable: "Compounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffMembers_Compounds_CompoundId",
                table: "StaffMembers",
                column: "CompoundId",
                principalTable: "Compounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceVendors_Compounds_CompoundId",
                table: "ServiceVendors");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffMembers_Compounds_CompoundId",
                table: "StaffMembers");

            migrationBuilder.DropIndex(
                name: "IX_StaffMembers_CompoundId",
                table: "StaffMembers");

            migrationBuilder.DropIndex(
                name: "IX_ServiceVendors_CompoundId",
                table: "ServiceVendors");

            migrationBuilder.DropColumn(
                name: "CompoundId",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "CompoundId",
                table: "ServiceVendors");
        }
    }
}
