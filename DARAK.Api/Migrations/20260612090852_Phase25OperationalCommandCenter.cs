using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase25OperationalCommandCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationalTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskType = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RelatedEntityType = table.Column<int>(type: "int", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalTasks_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalTasks_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalTasks_AspNetUsers_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalTasks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalTasks_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_AssignedToUserId",
                table: "OperationalTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_CancelledByUserId",
                table: "OperationalTasks",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_CompletedByUserId",
                table: "OperationalTasks",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_CompoundId",
                table: "OperationalTasks",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_CompoundId_Status_Priority_DueAtUtc",
                table: "OperationalTasks",
                columns: new[] { "CompoundId", "Status", "Priority", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_CreatedAtUtc",
                table: "OperationalTasks",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_CreatedByUserId",
                table: "OperationalTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_DueAtUtc",
                table: "OperationalTasks",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_Priority",
                table: "OperationalTasks",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_RelatedEntityType_RelatedEntityId",
                table: "OperationalTasks",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_Status",
                table: "OperationalTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalTasks_TaskType",
                table: "OperationalTasks",
                column: "TaskType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalTasks");
        }
    }
}
