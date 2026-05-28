// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokenPermissionEntries_RefreshTokenId_Resource_Permi~",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries");

            migrationBuilder.DropColumn(
                name: "Permission",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries");

            migrationBuilder.AddColumn<string[]>(
                name: "Permissions",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenPermissionEntries_RefreshTokenId_Resource",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries",
                columns: new[] { "RefreshTokenId", "Resource" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokenPermissionEntries_RefreshTokenId_Resource",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries");

            migrationBuilder.DropColumn(
                name: "Permissions",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries");

            migrationBuilder.AddColumn<string>(
                name: "Permission",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenPermissionEntries_RefreshTokenId_Resource_Permi~",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries",
                columns: new[] { "RefreshTokenId", "Resource", "Permission" },
                unique: true);
        }
    }
}
