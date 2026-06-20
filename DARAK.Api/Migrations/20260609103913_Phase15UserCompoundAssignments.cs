using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase15UserCompoundAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCompoundAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompoundAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompoundAssignments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCompoundAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCompoundAssignments_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompoundAssignments_CompoundId",
                table: "UserCompoundAssignments",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompoundAssignments_CreatedByUserId",
                table: "UserCompoundAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompoundAssignments_Role",
                table: "UserCompoundAssignments",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompoundAssignments_UserId",
                table: "UserCompoundAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompoundAssignments_UserId_CompoundId_Role",
                table: "UserCompoundAssignments",
                columns: new[] { "UserId", "CompoundId", "Role" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCompoundAssignments");
        }
    }
}
