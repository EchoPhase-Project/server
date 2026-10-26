// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Migrations
{
    /// <summary>A workflow's runs, newest first: the index of its run history.</summary>
    public class M20261026CreateWorkflowRuns : IMigration
    {
        public string Id => "20261026_create_workflow_runs";

        public Task Up(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m =>
                // Rows expire with their runs (same TTL): time-window compaction.
                m.CreateTable("workflow_runs", t => t
                        .Uuid("workflow_id")
                        .Uuid("run_id")
                        .Uuid("user_id")
                        .PrimaryKey("workflow_id", "run_id")
                        .ClusterKey("run_id", descending: true)
                        .WithCompaction("TimeWindowCompactionStrategy",
                            ("compaction_window_unit", "DAYS"), ("compaction_window_size", "7")),
                    ifNotExists: true), ct);

        public Task Down(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m => m.DropTable("workflow_runs", ifExists: true), ct);

        public async Task<bool> Validate(ScyllaDatabase db, CancellationToken ct = default)
        {
            var rows = await db.ExecuteQueryAsync(
                "SELECT table_name FROM system_schema.tables WHERE keyspace_name = ?",
                [db.Keyspace],
                ct);
            return rows.Any(r => r.GetValue<string>("table_name") == "workflow_runs");
        }
    }
}
