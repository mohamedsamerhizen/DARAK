using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase12CommunicationPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllowsMultipleChoices = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPolls_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResidentNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidentNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResidentNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementReadReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementReadReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementReadReceipts_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnouncementReadReceipts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPollOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPollOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPollOptions_CommunityPolls_PollId",
                        column: x => x.PollId,
                        principalTable: "CommunityPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPollVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPollVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPollVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityPollVotes_CommunityPollOptions_PollOptionId",
                        column: x => x.PollOptionId,
                        principalTable: "CommunityPollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityPollVotes_CommunityPolls_PollId",
                        column: x => x.PollId,
                        principalTable: "CommunityPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_AnnouncementId_UserId",
                table: "AnnouncementReadReceipts",
                columns: new[] { "AnnouncementId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_UserId",
                table: "AnnouncementReadReceipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Category",
                table: "Announcements",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedByUserId",
                table: "Announcements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_ExpiresAt",
                table: "Announcements",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_IsActive",
                table: "Announcements",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Priority",
                table: "Announcements",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Status",
                table: "Announcements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollOptions_PollId",
                table: "CommunityPollOptions",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollOptions_PollId_DisplayOrder",
                table: "CommunityPollOptions",
                columns: new[] { "PollId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_CreatedByUserId",
                table: "CommunityPolls",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_EndsAt",
                table: "CommunityPolls",
                column: "EndsAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_StartsAt",
                table: "CommunityPolls",
                column: "StartsAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_Status",
                table: "CommunityPolls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_PollId",
                table: "CommunityPollVotes",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_PollId_PollOptionId_UserId",
                table: "CommunityPollVotes",
                columns: new[] { "PollId", "PollOptionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_PollId_UserId",
                table: "CommunityPollVotes",
                columns: new[] { "PollId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_PollOptionId",
                table: "CommunityPollVotes",
                column: "PollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPollVotes_UserId",
                table: "CommunityPollVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentNotifications_CreatedAt",
                table: "ResidentNotifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentNotifications_IsRead",
                table: "ResidentNotifications",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentNotifications_UserId",
                table: "ResidentNotifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementReadReceipts");

            migrationBuilder.DropTable(
                name: "CommunityPollVotes");

            migrationBuilder.DropTable(
                name: "ResidentNotifications");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "CommunityPollOptions");

            migrationBuilder.DropTable(
                name: "CommunityPolls");
        }
    }
}
