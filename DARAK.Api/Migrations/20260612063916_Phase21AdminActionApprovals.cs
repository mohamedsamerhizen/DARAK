using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase21AdminActionApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowSelfApproval = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPriority = table.Column<int>(type: "int", nullable: false),
                    ExpireAfterHours = table.Column<int>(type: "int", nullable: false),
                    RequiredApproverRoles = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalPolicies_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastDecisionByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ExecutionStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestPayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExecutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_AspNetUsers_ExecutedByUserId",
                        column: x => x.ExecutedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_AspNetUsers_LastDecisionByUserId",
                        column: x => x.LastDecisionByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ApprovalRequestId",
                table: "ApprovalDecisions",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_CreatedAtUtc",
                table: "ApprovalDecisions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_DecidedByUserId",
                table: "ApprovalDecisions",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_DecisionType",
                table: "ApprovalDecisions",
                column: "DecisionType");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_ActionType",
                table: "ApprovalPolicies",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_CompoundId",
                table: "ApprovalPolicies",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_CompoundId_ActionType",
                table: "ApprovalPolicies",
                columns: new[] { "CompoundId", "ActionType" },
                unique: true,
                filter: "[CompoundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_IsEnabled",
                table: "ApprovalPolicies",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ActionType",
                table: "ApprovalRequests",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CompoundId",
                table: "ApprovalRequests",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CompoundId_Status_Priority",
                table: "ApprovalRequests",
                columns: new[] { "CompoundId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CreatedAtUtc",
                table: "ApprovalRequests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_DueAtUtc",
                table: "ApprovalRequests",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_EntityType_EntityId",
                table: "ApprovalRequests",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ExecutedByUserId",
                table: "ApprovalRequests",
                column: "ExecutedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ExecutionStatus",
                table: "ApprovalRequests",
                column: "ExecutionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_LastDecisionByUserId",
                table: "ApprovalRequests",
                column: "LastDecisionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Priority",
                table: "ApprovalRequests",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequestedByUserId",
                table: "ApprovalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Status",
                table: "ApprovalRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDecisions");

            migrationBuilder.DropTable(
                name: "ApprovalPolicies");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");
        }
    }
}
