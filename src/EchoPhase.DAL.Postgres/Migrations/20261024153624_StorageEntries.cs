using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class StorageEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageEntries",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "jsonb", nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageEntries_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StorageEntries_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "EchoPhase",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageEntries_ExpiresAt",
                schema: "EchoPhase",
                table: "StorageEntries",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_StorageEntries_UserId_WorkflowId_Key",
                schema: "EchoPhase",
                table: "StorageEntries",
                columns: new[] { "UserId", "WorkflowId", "Key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_StorageEntries_WorkflowId",
                schema: "EchoPhase",
                table: "StorageEntries",
                column: "WorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorageEntries",
                schema: "EchoPhase");
        }
    }
}
