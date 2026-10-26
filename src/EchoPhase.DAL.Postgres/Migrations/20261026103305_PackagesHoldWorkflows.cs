using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoPhase.DAL.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PackagesHoldWorkflows : Migration
    {
        // 1. Keys are the ids: what programs called (ExecuteWorkflow's
        //    workflowId, errorWorkflowId) keeps meaning the same workflow.
        // 2. Installed copies: one package per user and source package.
        // 3. The author's workflows go into the package they published them
        //    in (by the keys of its latest version); those that call or are
        //    called by another of the user's (ExecuteWorkflow, error workflow)
        //    into one package "Workflows" per user, so the calls keep working;
        //    the rest each into a package of its own (the workflow's id as
        //    the package's).
        // 4. A program's error workflow becomes its package's when it is in
        //    the package; programs (saved, versions, published) lose
        //    errorWorkflowId, ExecuteWorkflow's workflowId becomes workflow,
        //    Storage's scope User becomes Package.
        // 5. Storage: workflow values go with the workflow's package; values
        //    shared by all of a user's workflows have no package to go to and go.
        private const string MoveIntoPackages = """
            UPDATE "EchoPhase"."Workflows" SET "Key" = "Id";

            CREATE TEMP TABLE installed ON COMMIT DROP AS
                SELECT "UserId", "SourcePackageId", (array_agg("Id" ORDER BY "Id"))[1] AS "PackageId",
                       max("SourcePackageVersion") AS "SourceVersion"
                FROM "EchoPhase"."Workflows" WHERE "SourcePackageId" IS NOT NULL
                GROUP BY "UserId", "SourcePackageId";

            INSERT INTO "EchoPhase"."WorkflowPackages"
                ("Id", "OwnerId", "Name", "Summary", "Readme", "Visibility", "Tags", "LatestVersion",
                 "SourcePackageId", "SourceVersion", "ConcurrencyStamp", "UpdatedAt", "CreatedAt")
            SELECT i."PackageId", i."UserId", coalesce(s."Name", 'Installed'), coalesce(s."Summary", ''),
                   coalesce(s."Readme", ''), 'Private', '{}', 0, s."Id", i."SourceVersion", gen_random_uuid(), now(), now()
            FROM installed i LEFT JOIN "EchoPhase"."WorkflowPackages" s ON s."Id" = i."SourcePackageId";

            UPDATE "EchoPhase"."Workflows" w SET "PackageId" = i."PackageId"
            FROM installed i WHERE w."UserId" = i."UserId" AND w."SourcePackageId" = i."SourcePackageId";

            UPDATE "EchoPhase"."Workflows" w SET "PackageId" = p."Id"
            FROM "EchoPhase"."WorkflowPackages" p
            JOIN "EchoPhase"."WorkflowPackageVersions" v ON v."PackageId" = p."Id" AND v."Version" = p."LatestVersion"
            WHERE w."PackageId" = '00000000-0000-0000-0000-000000000000' AND p."OwnerId" = w."UserId"
              AND v."Content"->'workflows' @> jsonb_build_array(jsonb_build_object('key', w."Id"::text));

            CREATE TEMP TABLE linked ON COMMIT DROP AS
                SELECT w."Id", w."UserId" FROM "EchoPhase"."Workflows" w
                WHERE w."PackageId" = '00000000-0000-0000-0000-000000000000' AND EXISTS (
                    SELECT 1 FROM "EchoPhase"."Workflows" o
                    WHERE o."UserId" = w."UserId" AND o."Id" <> w."Id"
                      AND o."PackageId" = '00000000-0000-0000-0000-000000000000'
                      AND (strpos(o."Definition"::text, w."Id"::text) > 0 OR strpos(w."Definition"::text, o."Id"::text) > 0));

            CREATE TEMP TABLE linked_packages ON COMMIT DROP AS
                SELECT "UserId", (array_agg("Id" ORDER BY "Id"))[1] AS "PackageId" FROM linked GROUP BY "UserId";

            INSERT INTO "EchoPhase"."WorkflowPackages"
                ("Id", "OwnerId", "Name", "Summary", "Readme", "Visibility", "Tags", "LatestVersion",
                 "ConcurrencyStamp", "UpdatedAt", "CreatedAt")
            SELECT "PackageId", "UserId", 'Workflows', '', '', 'Private', '{}', 0, gen_random_uuid(), now(), now()
            FROM linked_packages;

            UPDATE "EchoPhase"."Workflows" w SET "PackageId" = p."PackageId"
            FROM linked l JOIN linked_packages p ON p."UserId" = l."UserId" WHERE w."Id" = l."Id";

            INSERT INTO "EchoPhase"."WorkflowPackages"
                ("Id", "OwnerId", "Name", "Summary", "Readme", "Visibility", "Tags", "LatestVersion",
                 "ConcurrencyStamp", "UpdatedAt", "CreatedAt")
            SELECT "Id", "UserId", "Name", '', '', 'Private', '{}', 0, gen_random_uuid(), "UpdatedAt", "CreatedAt"
            FROM "EchoPhase"."Workflows" WHERE "PackageId" = '00000000-0000-0000-0000-000000000000';

            UPDATE "EchoPhase"."Workflows" SET "PackageId" = "Id"
            WHERE "PackageId" = '00000000-0000-0000-0000-000000000000';

            UPDATE "EchoPhase"."WorkflowPackages" p SET "ErrorWorkflowKey" = e."Id"
            FROM "EchoPhase"."Workflows" w
            JOIN "EchoPhase"."Workflows" e ON e."Id"::text = w."Definition"->'settings'->>'errorWorkflowId'
            WHERE w."PackageId" = p."Id" AND e."PackageId" = p."Id";

            CREATE FUNCTION "EchoPhase".packages_fix_program(program jsonb) RETURNS jsonb LANGUAGE sql IMMUTABLE AS $$
                SELECT CASE WHEN jsonb_typeof(program->'blocks') = 'array'
                    THEN jsonb_set(program #- '{settings,errorWorkflowId}', '{blocks}', coalesce((
                        SELECT jsonb_agg(CASE
                            WHEN b->>'type' = 'ExecuteWorkflow' AND b->'params' ? 'workflowId'
                                THEN jsonb_set(b, '{params}', ((b->'params') - 'workflowId')
                                    || jsonb_build_object('workflow', b->'params'->'workflowId'))
                            WHEN b->>'type' = 'Storage' AND lower(b->'params'->>'scope') = 'user'
                                THEN jsonb_set(b, '{params,scope}', '"Package"')
                            ELSE b END ORDER BY ord)
                        FROM jsonb_array_elements(program->'blocks') WITH ORDINALITY AS t(b, ord)), '[]'))
                    ELSE program #- '{settings,errorWorkflowId}' END
            $$;

            UPDATE "EchoPhase"."Workflows" SET "Definition" = "EchoPhase".packages_fix_program("Definition");
            UPDATE "EchoPhase"."WorkflowVersions" SET "Definition" = "EchoPhase".packages_fix_program("Definition");
            UPDATE "EchoPhase"."WorkflowPackageVersions" SET "Content" = jsonb_set("Content", '{workflows}', coalesce((
                SELECT jsonb_agg(CASE WHEN w ? 'program'
                    THEN jsonb_set(w, '{program}', "EchoPhase".packages_fix_program(w->'program')) ELSE w END ORDER BY ord)
                FROM jsonb_array_elements("Content"->'workflows') WITH ORDINALITY AS t(w, ord)), '[]'))
            WHERE jsonb_typeof("Content"->'workflows') = 'array';

            DROP FUNCTION "EchoPhase".packages_fix_program(jsonb);

            DELETE FROM "EchoPhase"."StorageEntries" WHERE "WorkflowId" IS NULL;
            UPDATE "EchoPhase"."StorageEntries" s SET "PackageId" = w."PackageId"
            FROM "EchoPhase"."Workflows" w WHERE w."Id" = s."WorkflowId";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workflows_UserId_SourcePackageId",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_StorageEntries_UserId_WorkflowId_Key",
                schema: "EchoPhase",
                table: "StorageEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "Key",
                schema: "EchoPhase",
                table: "Workflows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PackageId",
                schema: "EchoPhase",
                table: "Workflows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ErrorWorkflowKey",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceVersion",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PackageId",
                schema: "EchoPhase",
                table: "StorageEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Every workflow into a package, before the columns that say where
            // installed copies came from go.
            migrationBuilder.Sql(MoveIntoPackages);

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

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_PackageId_Key",
                schema: "EchoPhase",
                table: "Workflows",
                columns: new[] { "PackageId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackages_OwnerId_SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                columns: new[] { "OwnerId", "SourcePackageId" },
                filter: "\"SourcePackageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowPackages_SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                column: "SourcePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageEntries_PackageId_WorkflowId_Key",
                schema: "EchoPhase",
                table: "StorageEntries",
                columns: new[] { "PackageId", "WorkflowId", "Key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_StorageEntries_UserId",
                schema: "EchoPhase",
                table: "StorageEntries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_StorageEntries_WorkflowPackages_PackageId",
                schema: "EchoPhase",
                table: "StorageEntries",
                column: "PackageId",
                principalSchema: "EchoPhase",
                principalTable: "WorkflowPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowPackages_WorkflowPackages_SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages",
                column: "SourcePackageId",
                principalSchema: "EchoPhase",
                principalTable: "WorkflowPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Workflows_WorkflowPackages_PackageId",
                schema: "EchoPhase",
                table: "Workflows",
                column: "PackageId",
                principalSchema: "EchoPhase",
                principalTable: "WorkflowPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StorageEntries_WorkflowPackages_PackageId",
                schema: "EchoPhase",
                table: "StorageEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowPackages_WorkflowPackages_SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_Workflows_WorkflowPackages_PackageId",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_PackageId_Key",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowPackages_OwnerId_SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowPackages_SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages");

            migrationBuilder.DropIndex(
                name: "IX_StorageEntries_PackageId_WorkflowId_Key",
                schema: "EchoPhase",
                table: "StorageEntries");

            migrationBuilder.DropIndex(
                name: "IX_StorageEntries_UserId",
                schema: "EchoPhase",
                table: "StorageEntries");

            migrationBuilder.DropColumn(
                name: "Key",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "PackageId",
                schema: "EchoPhase",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "ErrorWorkflowKey",
                schema: "EchoPhase",
                table: "WorkflowPackages");

            migrationBuilder.DropColumn(
                name: "SourcePackageId",
                schema: "EchoPhase",
                table: "WorkflowPackages");

            migrationBuilder.DropColumn(
                name: "SourceVersion",
                schema: "EchoPhase",
                table: "WorkflowPackages");

            migrationBuilder.DropColumn(
                name: "PackageId",
                schema: "EchoPhase",
                table: "StorageEntries");

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

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_UserId_SourcePackageId",
                schema: "EchoPhase",
                table: "Workflows",
                columns: new[] { "UserId", "SourcePackageId" },
                filter: "\"SourcePackageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StorageEntries_UserId_WorkflowId_Key",
                schema: "EchoPhase",
                table: "StorageEntries",
                columns: new[] { "UserId", "WorkflowId", "Key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
