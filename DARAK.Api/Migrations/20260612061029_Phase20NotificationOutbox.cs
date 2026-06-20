using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase20NotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RecipientPhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RelatedEntityType = table.Column<int>(type: "int", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRetryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationOutbox_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationOutbox_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationOutbox_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationOutbox_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationOutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryAttempts_NotificationOutbox_NotificationOutboxId",
                        column: x => x.NotificationOutboxId,
                        principalTable: "NotificationOutbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_NotificationOutboxId",
                table: "NotificationDeliveryAttempts",
                column: "NotificationOutboxId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_StartedAtUtc",
                table: "NotificationDeliveryAttempts",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_Status",
                table: "NotificationDeliveryAttempts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Channel",
                table: "NotificationOutbox",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_CompoundId",
                table: "NotificationOutbox",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_CreatedAtUtc",
                table: "NotificationOutbox",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_CreatedByUserId",
                table: "NotificationOutbox",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_EventType",
                table: "NotificationOutbox",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_NextRetryAtUtc",
                table: "NotificationOutbox",
                column: "NextRetryAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Priority",
                table: "NotificationOutbox",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_RecipientUserId",
                table: "NotificationOutbox",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_RelatedEntityType_RelatedEntityId",
                table: "NotificationOutbox",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_ResidentProfileId",
                table: "NotificationOutbox",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_ScheduledAtUtc",
                table: "NotificationOutbox",
                column: "ScheduledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status",
                table: "NotificationOutbox",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Channel",
                table: "NotificationTemplates",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Code",
                table: "NotificationTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_EventType",
                table: "NotificationTemplates",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_IsActive",
                table: "NotificationTemplates",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "NotificationOutbox");
        }
    }
}
