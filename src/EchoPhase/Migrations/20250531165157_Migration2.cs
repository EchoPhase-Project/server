using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.Migrations
{
    /// <inheritdoc />
    public partial class Migration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "WebHooks",
                schema: "EchoPhase",
                newName: "WebHooks",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "EchoPhase",
                newName: "Users",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "Roles",
                schema: "EchoPhase",
                newName: "Roles",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "JwtTokens",
                schema: "EchoPhase",
                newName: "JwtTokens",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "DiscordTokens",
                schema: "EchoPhase",
                newName: "DiscordTokens",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "EchoPhase",
                newName: "AspNetUserTokens",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "EchoPhase",
                newName: "AspNetUserRoles",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "EchoPhase",
                newName: "AspNetUserLogins",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "EchoPhase",
                newName: "AspNetUserClaims",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "EchoPhase",
                newName: "AspNetRoleClaims",
                newSchema: "public");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "public",
                table: "WebHooks",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                schema: "public",
                table: "WebHooks");

            migrationBuilder.EnsureSchema(
                name: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "WebHooks",
                schema: "public",
                newName: "WebHooks",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "public",
                newName: "Users",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "Roles",
                schema: "public",
                newName: "Roles",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "JwtTokens",
                schema: "public",
                newName: "JwtTokens",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "DiscordTokens",
                schema: "public",
                newName: "DiscordTokens",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "public",
                newName: "AspNetUserTokens",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "public",
                newName: "AspNetUserRoles",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "public",
                newName: "AspNetUserLogins",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "public",
                newName: "AspNetUserClaims",
                newSchema: "EchoPhase");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "public",
                newName: "AspNetRoleClaims",
                newSchema: "EchoPhase");
        }
    }
}
