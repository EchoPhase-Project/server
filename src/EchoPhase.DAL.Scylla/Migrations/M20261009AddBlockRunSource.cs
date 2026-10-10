// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Migrations
{
    /// <summary>Where a run came from: a saved workflow and what started it.</summary>
    public class M20261009AddBlockRunSource : IMigration
    {
        private static readonly string[] Columns = ["workflow_id", "trigger"];

        public string Id => "20261009_add_block_run_source";

        public Task Up(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m => m.AlterTable("block_runs", t => t
                .AddColumn("workflow_id", "uuid")
                .AddColumn("trigger", "text")), ct);

        public Task Down(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m => m.AlterTable("block_runs", t => t
                .DropColumn("workflow_id")
                .DropColumn("trigger")), ct);

        public async Task<bool> Validate(ScyllaDatabase db, CancellationToken ct = default)
        {
            var rows = await db.ExecuteQueryAsync(
                "SELECT column_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ?",
                [db.Keyspace, "block_runs"],
                ct);

            var existing = rows.Select(r => r.GetValue<string>("column_name")).ToHashSet();
            return Columns.All(existing.Contains);
        }
    }
}
