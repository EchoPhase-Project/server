using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokenAudits",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "RefreshTokenPermissionEntries",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "EchoPhase");

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    Scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    Intents = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionAudits",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionAudits_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "EchoPhase",
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionPermissionEntries",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPermissionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionPermissionEntries_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "EchoPhase",
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionAudits_SessionId",
                schema: "EchoPhase",
                table: "SessionAudits",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionAudits_TokenHash",
                schema: "EchoPhase",
                table: "SessionAudits",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionPermissionEntries_SessionId_Resource",
                schema: "EchoPhase",
                table: "SessionPermissionEntries",
                columns: new[] { "SessionId", "Resource" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RefreshTokenHash",
                schema: "EchoPhase",
                table: "Sessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                schema: "EchoPhase",
                table: "Sessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionAudits",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "SessionPermissionEntries",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "EchoPhase");

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeviceId = table.Column<string>(type: "text", nullable: true),
                    Intents = table.Column<string[]>(type: "text[]", nullable: false),
                    RefreshValue = table.Column<string>(type: "text", nullable: false),
                    Scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokenAudits",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Token = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokenAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokenAudits_RefreshTokens_RefreshTokenId",
                        column: x => x.RefreshTokenId,
                        principalSchema: "EchoPhase",
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokenPermissionEntries",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokenPermissionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokenPermissionEntries_RefreshTokens_RefreshTokenId",
                        column: x => x.RefreshTokenId,
                        principalSchema: "EchoPhase",
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenAudits_RefreshTokenId",
                schema: "EchoPhase",
                table: "RefreshTokenAudits",
                column: "RefreshTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenPermissionEntries_RefreshTokenId_Resource",
                schema: "EchoPhase",
                table: "RefreshTokenPermissionEntries",
                columns: new[] { "RefreshTokenId", "Resource" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_RefreshValue_UserId",
                schema: "EchoPhase",
                table: "RefreshTokens",
                columns: new[] { "RefreshValue", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                schema: "EchoPhase",
                table: "RefreshTokens",
                column: "UserId");
        }
    }
}
