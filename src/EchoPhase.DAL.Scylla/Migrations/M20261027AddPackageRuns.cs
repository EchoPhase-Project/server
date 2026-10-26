// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Migrations
{
    /// <summary>The package a run's workflow belongs to, and a package's runs, newest first.</summary>
    public class M20261027AddPackageRuns : IMigration
    {
        public string Id => "20261027_add_package_runs";

        public async Task Up(ScyllaDatabase db, CancellationToken ct = default)
        {
            if (!await HasColumnAsync(db, ct))
                await db.ApplyMigrationAsync(m => m.AlterTable("block_runs", t => t.AddColumn("package_id", "uuid")), ct);
            // Rows expire with their runs (same TTL): time-window compaction.
            await db.ApplyMigrationAsync(m =>
                m.CreateTable("package_runs", t => t
                        .Uuid("package_id")
                        .Uuid("run_id")
                        .Uuid("user_id")
                        .PrimaryKey("package_id", "run_id")
                        .ClusterKey("run_id", descending: true)
                        .WithCompaction("TimeWindowCompactionStrategy",
                            ("compaction_window_unit", "DAYS"), ("compaction_window_size", "7")),
                    ifNotExists: true), ct);
        }

        // Survives a half-applied Up: each step only if it is there.
        public async Task Down(ScyllaDatabase db, CancellationToken ct = default)
        {
            await db.ApplyMigrationAsync(m => m.DropTable("package_runs", ifExists: true), ct);
            if (await HasColumnAsync(db, ct))
                await db.ApplyMigrationAsync(m => m.AlterTable("block_runs", t => t.DropColumn("package_id")), ct);
        }

        public async Task<bool> Validate(ScyllaDatabase db, CancellationToken ct = default)
        {
            var rows = await db.ExecuteQueryAsync(
                "SELECT table_name FROM system_schema.tables WHERE keyspace_name = ?",
                [db.Keyspace],
                ct);
            return rows.Any(r => r.GetValue<string>("table_name") == "package_runs") && await HasColumnAsync(db, ct);
        }

        private static async Task<bool> HasColumnAsync(ScyllaDatabase db, CancellationToken ct)
        {
            var rows = await db.ExecuteQueryAsync(
                "SELECT column_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ?",
                [db.Keyspace, "block_runs"],
                ct);
            return rows.Any(r => r.GetValue<string>("column_name") == "package_id");
        }
    }
}
