using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceKey",
                schema: "EchoPhase",
                table: "Workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePackageId",
                schema: "EchoPhase",
                table: "Workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourcePackageVersion",
                schema: "EchoPhase",
                table: "Workflows",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowPackages",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Readme = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    LatestVersion = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowPackages_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowPackageInstalls",
                schema: "EchoPhase",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    InstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowPackageInstalls", x => new { x.PackageId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkflowPackageInstalls_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowPackageInstalls_WorkflowPackages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "EchoPhase",
                        principalTable: "WorkflowPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowPackageShares",
                schema: "EchoPhase",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowPackageShares", x => new { x.PackageId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkflowPackageShares_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowPackageShares_WorkflowPackages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "EchoPhase",
                        principalTable: "WorkflowPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowPackageVersions",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Changelog = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowPackageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowPackageVersions_WorkflowPackages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "EchoPhase",
                        principalTable: "WorkflowPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_UserId_SourcePackageId",
                schema: "EchoPhase",
                table: "Workflows",
                columns: new[] { "UserId", "SourcePackageId" },
                filter: "\"SourcePackageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackageInstalls_UserId",
                schema: "EchoPhase",
                table: "WorkflowPackageInstalls",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackages_OwnerId",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackages_Tags",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackages_Visibility",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackageShares_UserId",
                schema: "EchoPhase",
                table: "WorkflowPackageShares",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackageVersions_PackageId_Version",
                schema: "EchoPhase",
                table: "WorkflowPackageVersions",
                columns: new[] { "PackageId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowPackageInstalls",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "WorkflowPackageShares",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "WorkflowPackageVersions",
                schema: "EchoPhase");

            migrationBuilder.DropTable(
                name: "WorkflowPackages",
                schema: "EchoPhase");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_UserId_SourcePackageId",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "SourcePackageId",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "SourcePackageVersion",
                schema: "EchoPhase",
                table: "Workflows");
        }
    }
}
