using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DARAK.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase5AOwnershipTransferPropertyUnitIndexFixFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_OwnershipTransferRequests_PropertyUnitId'
                      AND object_id = OBJECT_ID(N'[OwnershipTransferRequests]')
                )
                BEGIN
                    CREATE INDEX [IX_OwnershipTransferRequests_PropertyUnitId]
                    ON [OwnershipTransferRequests] ([PropertyUnitId]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
