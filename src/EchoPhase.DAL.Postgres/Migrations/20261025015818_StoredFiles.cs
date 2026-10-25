using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class StoredFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileImageName",
                schema: "EchoPhase",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "AvatarFileId",
                schema: "EchoPhase",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                schema: "EchoPhase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoredFiles_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "EchoPhase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_AvatarFileId",
                schema: "EchoPhase",
                table: "Users",
                column: "AvatarFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerId_Purpose_Id",
                schema: "EchoPhase",
                table: "StoredFiles",
                columns: new[] { "OwnerId", "Purpose", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_Status_CreatedAt",
                schema: "EchoPhase",
                table: "StoredFiles",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_StorageKey",
                schema: "EchoPhase",
                table: "StoredFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_StoredFiles_AvatarFileId",
                schema: "EchoPhase",
                table: "Users",
                column: "AvatarFileId",
                principalSchema: "EchoPhase",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_StoredFiles_AvatarFileId",
                schema: "EchoPhase",
                table: "Users");

            migrationBuilder.DropTable(
                name: "StoredFiles",
                schema: "EchoPhase");

            migrationBuilder.DropIndex(
                name: "IX_Users_AvatarFileId",
                schema: "EchoPhase",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvatarFileId",
                schema: "EchoPhase",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageName",
                schema: "EchoPhase",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
