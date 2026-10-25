using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Notes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notes",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    Pinned = table.Column<bool>(type: "boolean", nullable: false),
                    RemindAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteShares",
                schema: "EchoPhase",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteShares", x => new { x.NoteId, x.UserId });
                    table.ForeignKey(
                        name: "FK_NoteShares_Notes_NoteId",
                        column: x => x.NoteId,
                        principalSchema: "EchoPhase",
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoteShares_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_OwnerId_Pinned_UpdatedAt",
                schema: "EchoPhase",
                table: "Notes",
                columns: new[] { "OwnerId", "Pinned", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_RemindAt",
                schema: "EchoPhase",
                table: "Notes",
                column: "RemindAt",
                filter: "\"RemindAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NoteShares_UserId",
                schema: "EchoPhase",
                table: "NoteShares",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteShares",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "Notes",
                schema: "EchoPhase");
        }
    }
}
