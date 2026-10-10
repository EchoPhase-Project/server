using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old column held versioned bitmasks that cannot be turned into
            // names in SQL (and stopped matching once the intent set changed):
            // webhooks start with no intents and are re-subscribed.
            migrationBuilder.DropColumn(
                name: "Intents",
                schema: "EchoPhase",
                table: "WebHooks");

            migrationBuilder.AddColumn<List<string>>(
                name: "Intents",
                schema: "EchoPhase",
                table: "WebHooks",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.CreateIndex(
                name: "IX_WebHooks_Intents",
                schema: "EchoPhase",
                table: "WebHooks",
                column: "Intents")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebHooks_Intents",
                schema: "EchoPhase",
                table: "WebHooks");

            migrationBuilder.DropColumn(
                name: "Intents",
                schema: "EchoPhase",
                table: "WebHooks");

            migrationBuilder.AddColumn<string>(
                name: "Intents",
                schema: "EchoPhase",
                table: "WebHooks",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
