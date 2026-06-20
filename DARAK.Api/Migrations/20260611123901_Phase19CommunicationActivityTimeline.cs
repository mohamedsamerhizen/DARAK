using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase19CommunicationActivityTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Topic = table.Column<int>(type: "int", nullable: false),
                    IssueType = table.Column<int>(type: "int", nullable: false),
                    LinkedEntityType = table.Column<int>(type: "int", nullable: false),
                    LinkedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAssignmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    LastReopenReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReopenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedByResidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastResidentMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAdminMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_AspNetUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_AspNetUsers_EscalatedByUserId",
                        column: x => x.EscalatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_ResidentProfiles_ReopenedByResidentId",
                        column: x => x.ReopenedByResidentId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageType = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_ActorUserId",
                table: "ActivityEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_CompoundId",
                table: "ActivityEvents",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_CreatedAtUtc",
                table: "ActivityEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_EntityType_EntityId",
                table: "ActivityEvents",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_EventType",
                table: "ActivityEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_PropertyUnitId",
                table: "ActivityEvents",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_ResidentProfileId",
                table: "ActivityEvents",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId",
                table: "ConversationMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CreatedAtUtc",
                table: "ConversationMessages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_MessageType",
                table: "ConversationMessages",
                column: "MessageType");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SenderUserId",
                table: "ConversationMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_Visibility",
                table: "ConversationMessages",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedByUserId",
                table: "Conversations",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedToUserId",
                table: "Conversations",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompoundId",
                table: "Conversations",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CreatedAtUtc",
                table: "Conversations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_EscalatedByUserId",
                table: "Conversations",
                column: "EscalatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_EscalationLevel",
                table: "Conversations",
                column: "EscalationLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_IssueType",
                table: "Conversations",
                column: "IssueType");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LastMessageAtUtc",
                table: "Conversations",
                column: "LastMessageAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LinkedEntityType_LinkedEntityId",
                table: "Conversations",
                columns: new[] { "LinkedEntityType", "LinkedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Priority",
                table: "Conversations",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_PropertyUnitId",
                table: "Conversations",
                column: "PropertyUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ReopenedByResidentId",
                table: "Conversations",
                column: "ReopenedByResidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ResidentProfileId",
                table: "Conversations",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Status",
                table: "Conversations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Topic",
                table: "Conversations",
                column: "Topic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "Conversations");
        }
    }
}
