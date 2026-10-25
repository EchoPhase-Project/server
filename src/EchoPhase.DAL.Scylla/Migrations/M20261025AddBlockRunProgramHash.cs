// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Migrations
{
    /// <summary>The program a run ran, as its hash (BlockProgramHash).</summary>
    public class M20261025AddBlockRunProgramHash : IMigration
    {
        public string Id => "20261025_add_block_run_program_hash";

        public Task Up(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m => m.AlterTable("block_runs", t => t.AddColumn("program_hash", "text")), ct);

        public Task Down(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m => m.AlterTable("block_runs", t => t.DropColumn("program_hash")), ct);

        public async Task<bool> Validate(ScyllaDatabase db, CancellationToken ct = default)
        {
            var rows = await db.ExecuteQueryAsync(
                "SELECT column_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ?",
                [db.Keyspace, "block_runs"],
                ct);
            return rows.Any(r => r.GetValue<string>("column_name") == "program_hash");
        }
    }
}
