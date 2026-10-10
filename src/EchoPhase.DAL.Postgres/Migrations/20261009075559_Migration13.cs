using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPermissionGrants",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    GrantedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryApplied = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissionGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissionGrants_Users_GrantedById",
                        column: x => x.GrantedById,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserPermissionGrants_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionGrants_ExpiresAt",
                schema: "EchoPhase",
                table: "UserPermissionGrants",
                column: "ExpiresAt",
                filter: "\"ExpiresAt\" IS NOT NULL AND NOT \"ExpiryApplied\"");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionGrants_GrantedById",
                schema: "EchoPhase",
                table: "UserPermissionGrants",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionGrants_UserId_Resource",
                schema: "EchoPhase",
                table: "UserPermissionGrants",
                columns: new[] { "UserId", "Resource" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPermissionGrants",
                schema: "EchoPhase");
        }
    }
}
