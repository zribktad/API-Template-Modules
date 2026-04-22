using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Migrations
{
    /// <inheritdoc />
    public partial class UseNormalizedStringOwned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Composite indexes IX_Users_TenantId_NormalizedEmail, IX_Users_TenantId_NormalizedUsername,
            // IX_TenantInvitations_TenantId_NormalizedEmail are preserved in the DB as-is.
            // Email/Username are now OwnsOne(NormalizedString) — EF Core cannot model these
            // cross-owner composite indexes with OwnsOne, so they are managed as DB-only indexes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Composite indexes were never dropped in Up(), so nothing to recreate here.
        }
    }
}
