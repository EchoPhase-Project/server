using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.Migrations
{
    /// <inheritdoc />
    public partial class Migration6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_DeviceId",
                schema: "public",
                table: "RefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_DeviceId_UserId",
                schema: "public",
                table: "RefreshTokens",
                columns: new[] { "DeviceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_RefreshValue_DeviceId",
                schema: "public",
                table: "RefreshTokens",
                columns: new[] { "RefreshValue", "DeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_DeviceId_UserId",
                schema: "public",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_RefreshValue_DeviceId",
                schema: "public",
                table: "RefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_DeviceId",
                schema: "public",
                table: "RefreshTokens",
                column: "DeviceId",
                unique: true);
        }
    }
}
