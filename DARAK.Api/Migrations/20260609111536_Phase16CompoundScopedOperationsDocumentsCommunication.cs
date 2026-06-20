using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase16CompoundScopedOperationsDocumentsCommunication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompoundId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompoundId",
                table: "DocumentFiles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompoundId",
                table: "CommunityPolls",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompoundId",
                table: "Announcements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CompoundId",
                table: "WorkOrders",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_CompoundId",
                table: "DocumentFiles",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_CompoundId",
                table: "CommunityPolls",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CompoundId",
                table: "Announcements",
                column: "CompoundId");

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Compounds_CompoundId",
                table: "Announcements",
                column: "CompoundId",
                principalTable: "Compounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityPolls_Compounds_CompoundId",
                table: "CommunityPolls",
                column: "CompoundId",
                principalTable: "Compounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_Compounds_CompoundId",
                table: "DocumentFiles",
                column: "CompoundId",
                principalTable: "Compounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Compounds_CompoundId",
                table: "WorkOrders",
                column: "CompoundId",
                principalTable: "Compounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Compounds_CompoundId",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunityPolls_Compounds_CompoundId",
                table: "CommunityPolls");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_Compounds_CompoundId",
                table: "DocumentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Compounds_CompoundId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CompoundId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_CompoundId",
                table: "DocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_CommunityPolls_CompoundId",
                table: "CommunityPolls");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_CompoundId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "CompoundId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CompoundId",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "CompoundId",
                table: "CommunityPolls");

            migrationBuilder.DropColumn(
                name: "CompoundId",
                table: "Announcements");
        }
    }
}
