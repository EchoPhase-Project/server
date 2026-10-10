using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Migration9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scopes",
                schema: "EchoPhase",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Scopes",
                schema: "EchoPhase",
                table: "ApiClients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Scopes",
                schema: "EchoPhase",
                table: "Sessions",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "Scopes",
                schema: "EchoPhase",
                table: "ApiClients",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }
    }
}
