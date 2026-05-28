// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokenIntents",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "RefreshTokenScopes",
                schema: "EchoPhase");

            migrationBuilder.AddColumn<string[]>(
                name: "Intents",
                schema: "EchoPhase",
                table: "RefreshTokens",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "Scopes",
                schema: "EchoPhase",
                table: "RefreshTokens",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Intents",
                schema: "EchoPhase",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "Scopes",
                schema: "EchoPhase",
                table: "RefreshTokens");

            migrationBuilder.CreateTable(
                name: "RefreshTokenIntents",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokenIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokenIntents_RefreshTokens_RefreshTokenId",
                        column: x => x.RefreshTokenId,
                        principalSchema: "EchoPhase",
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokenScopes",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokenScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokenScopes_RefreshTokens_RefreshTokenId",
                        column: x => x.RefreshTokenId,
                        principalSchema: "EchoPhase",
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenIntents_RefreshTokenId_Value",
                schema: "EchoPhase",
                table: "RefreshTokenIntents",
                columns: new[] { "RefreshTokenId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenScopes_RefreshTokenId_Value",
                schema: "EchoPhase",
                table: "RefreshTokenScopes",
                columns: new[] { "RefreshTokenId", "Value" },
                unique: true);
        }
    }
}
