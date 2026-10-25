using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                schema: "EchoPhase",
                table: "WorkflowVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentVersion",
                schema: "EchoPhase",
                table: "WorkflowVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestoredFrom",
                schema: "EchoPhase",
                table: "WorkflowVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                schema: "EchoPhase",
                table: "Workflows",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVersions_WorkflowId_ContentHash",
                schema: "EchoPhase",
                table: "WorkflowVersions",
                columns: new[] { "WorkflowId", "ContentHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowVersions_WorkflowId_ContentHash",
                schema: "EchoPhase",
                table: "WorkflowVersions");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "EchoPhase",
                table: "WorkflowVersions");

            migrationBuilder.DropColumn(
                name: "ParentVersion",
                schema: "EchoPhase",
                table: "WorkflowVersions");

            migrationBuilder.DropColumn(
                name: "RestoredFrom",
                schema: "EchoPhase",
                table: "WorkflowVersions");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "EchoPhase",
                table: "Workflows");
        }
    }
}
