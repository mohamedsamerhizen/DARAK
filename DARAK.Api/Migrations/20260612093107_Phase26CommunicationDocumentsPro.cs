using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase26CommunicationDocumentsPro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "DocumentFiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "DocumentFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousVersionDocumentFileId",
                table: "DocumentFiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "DocumentFiles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "DocumentFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "DocumentFiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootDocumentFileId",
                table: "DocumentFiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                table: "DocumentFiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CommunicationCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    NotificationType = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetBuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetFloorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetPropertyUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    OutboxItemCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationCampaigns_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationCampaigns_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    AppliesTo = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRequirements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRequirements_Compounds_CompoundId",
                        column: x => x.CompoundId,
                        principalTable: "Compounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResidentNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BillNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PaymentNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MaintenanceNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ComplaintNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ViolationNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    VisitorNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DocumentNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AnnouncementNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CampaignNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DoNotDisturbEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DoNotDisturbStartLocalTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    DoNotDisturbEndLocalTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidentNotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidentNotificationPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationOutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliverySuppressed = table.Column<bool>(type: "bit", nullable: false),
                    SuppressionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationCampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationCampaignRecipients_CommunicationCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "CommunicationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationCampaignRecipients_NotificationOutbox_NotificationOutboxId",
                        column: x => x.NotificationOutboxId,
                        principalTable: "NotificationOutbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationCampaignRecipients_ResidentProfiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalTable: "ResidentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_ApprovalStatus",
                table: "DocumentFiles",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_ExpiresAtUtc",
                table: "DocumentFiles",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_PreviousVersionDocumentFileId",
                table: "DocumentFiles",
                column: "PreviousVersionDocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_ReviewedByUserId",
                table: "DocumentFiles",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_RootDocumentFileId",
                table: "DocumentFiles",
                column: "RootDocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaignRecipients_CampaignId",
                table: "CommunicationCampaignRecipients",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaignRecipients_CampaignId_ResidentProfileId",
                table: "CommunicationCampaignRecipients",
                columns: new[] { "CampaignId", "ResidentProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaignRecipients_DeliverySuppressed",
                table: "CommunicationCampaignRecipients",
                column: "DeliverySuppressed");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaignRecipients_NotificationOutboxId",
                table: "CommunicationCampaignRecipients",
                column: "NotificationOutboxId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaignRecipients_ResidentProfileId",
                table: "CommunicationCampaignRecipients",
                column: "ResidentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaignRecipients_UserId",
                table: "CommunicationCampaignRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_CompoundId",
                table: "CommunicationCampaigns",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_CreatedAtUtc",
                table: "CommunicationCampaigns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_CreatedByUserId",
                table: "CommunicationCampaigns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_SentAtUtc",
                table: "CommunicationCampaigns",
                column: "SentAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_Status",
                table: "CommunicationCampaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationCampaigns_TargetType",
                table: "CommunicationCampaigns",
                column: "TargetType");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_AppliesTo",
                table: "DocumentRequirements",
                column: "AppliesTo");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_Category",
                table: "DocumentRequirements",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_CompoundId",
                table: "DocumentRequirements",
                column: "CompoundId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_CompoundId_Category_AppliesTo_IsActive",
                table: "DocumentRequirements",
                columns: new[] { "CompoundId", "Category", "AppliesTo", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_CreatedByUserId",
                table: "DocumentRequirements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_IsActive",
                table: "DocumentRequirements",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_IsMandatory",
                table: "DocumentRequirements",
                column: "IsMandatory");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentNotificationPreferences_DoNotDisturbEnabled",
                table: "ResidentNotificationPreferences",
                column: "DoNotDisturbEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentNotificationPreferences_UserId",
                table: "ResidentNotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_AspNetUsers_ReviewedByUserId",
                table: "DocumentFiles",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_PreviousVersionDocumentFileId",
                table: "DocumentFiles",
                column: "PreviousVersionDocumentFileId",
                principalTable: "DocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_RootDocumentFileId",
                table: "DocumentFiles",
                column: "RootDocumentFileId",
                principalTable: "DocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_AspNetUsers_ReviewedByUserId",
                table: "DocumentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_PreviousVersionDocumentFileId",
                table: "DocumentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_RootDocumentFileId",
                table: "DocumentFiles");

            migrationBuilder.DropTable(
                name: "CommunicationCampaignRecipients");

            migrationBuilder.DropTable(
                name: "DocumentRequirements");

            migrationBuilder.DropTable(
                name: "ResidentNotificationPreferences");

            migrationBuilder.DropTable(
                name: "CommunicationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_ApprovalStatus",
                table: "DocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_ExpiresAtUtc",
                table: "DocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_PreviousVersionDocumentFileId",
                table: "DocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_ReviewedByUserId",
                table: "DocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_RootDocumentFileId",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "PreviousVersionDocumentFileId",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "RootDocumentFileId",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                table: "DocumentFiles");
        }
    }
}
