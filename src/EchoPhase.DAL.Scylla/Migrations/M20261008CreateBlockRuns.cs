// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Migrations
{
    public class M20261008CreateBlockRuns : IMigration
    {
        private static readonly string[] Tables = ["block_runs", "block_run_results", "block_runs_active"];

        public string Id => "20261008_create_block_runs";

        public Task Up(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m =>
            {
                // Rows expire by TTL and are read newest first: time-window
                // compaction drops whole expired SSTables instead of compacting them.
                m.CreateTable("block_runs", t => t
                        .Uuid("user_id")
                        .Uuid("run_id")
                        .Text("status")
                        .Uuid("actor_id")
                        .Int("block_count")
                        .Timestamp("created_at")
                        .Timestamp("started_at")
                        .Timestamp("finished_at")
                        .BigInt("duration_ms")
                        .Int("steps")
                        .Int("error_count")
                        .PrimaryKey("user_id", "run_id")
                        .ClusterKey("run_id", descending: true)
                        .WithCompaction("TimeWindowCompactionStrategy",
                            ("compaction_window_unit", "DAYS"), ("compaction_window_size", "7")),
                    ifNotExists: true);

                m.CreateTable("block_run_results", t => t
                        .Uuid("user_id")
                        .Uuid("run_id")
                        .Text("payload")
                        .PrimaryKey("user_id", "run_id")
                        .WithCompaction("TimeWindowCompactionStrategy",
                            ("compaction_window_unit", "HOURS"), ("compaction_window_size", "6")),
                    ifNotExists: true);

                m.CreateTable("block_runs_active", t => t
                        .Uuid("run_id")
                        .Uuid("user_id")
                        .PrimaryKey("run_id"),
                    ifNotExists: true);
            }, ct);

        public Task Down(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m =>
            {
                foreach (var table in Tables)
                    m.DropTable(table, ifExists: true);
            }, ct);

        public async Task<bool> Validate(ScyllaDatabase db, CancellationToken ct = default)
        {
            var rows = await db.ExecuteQueryAsync(
                "SELECT table_name FROM system_schema.tables WHERE keyspace_name = ?",
                [db.Keyspace],
                ct);

            var existing = rows.Select(r => r.GetValue<string>("table_name")).ToHashSet();
            return Tables.All(existing.Contains);
        }
    }
}
