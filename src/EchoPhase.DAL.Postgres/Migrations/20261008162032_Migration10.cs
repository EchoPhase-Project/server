using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionPermissionEntries",
                schema: "EchoPhase");

            migrationBuilder.DropColumn(
                name: "Intents",
                schema: "EchoPhase",
                table: "Sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Intents",
                schema: "EchoPhase",
                table: "Sessions",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "SessionPermissionEntries",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false)
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
                name: "IX_SessionPermissionEntries_SessionId_Resource",
                schema: "EchoPhase",
                table: "SessionPermissionEntries",
                columns: new[] { "SessionId", "Resource" },
                unique: true);
        }
    }
}
