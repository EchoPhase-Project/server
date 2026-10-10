using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowVersions",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    SavedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowVersions_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "EchoPhase",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVersions_WorkflowId_Version",
                schema: "EchoPhase",
                table: "WorkflowVersions",
                columns: new[] { "WorkflowId", "Version" },
                unique: true);

            // Existing workflows get their current state as their first snapshot.
            migrationBuilder.Sql("""
                INSERT INTO "EchoPhase"."WorkflowVersions" ("Id", "WorkflowId", "Version", "Name", "Definition", "SavedBy", "CreatedAt")
                SELECT gen_random_uuid(), "Id", "Version", "Name", "Definition", NULL, "UpdatedAt"
                FROM "EchoPhase"."Workflows";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowVersions",
                schema: "EchoPhase");
        }
    }
}
