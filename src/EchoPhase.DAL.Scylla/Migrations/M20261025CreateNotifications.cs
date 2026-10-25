// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Migrations
{
    /// <summary>Users' inboxes: notifications, newest first, expiring by TTL.</summary>
    public class M20261025CreateNotifications : IMigration
    {
        private const string Table = "notifications";

        public string Id => "20261025_create_notifications";

        public Task Up(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m =>
                // Written with a TTL and read newest first, like block runs.
                m.CreateTable(Table, t => t
                        .Uuid("user_id")
                        .Uuid("id")
                        .Text("kind")
                        .Text("title")
                        .Text("body")
                        .Text("link")
                        .Text("params")
                        .Boolean("read")
                        .Timestamp("created_at")
                        .PrimaryKey("user_id", "id")
                        .ClusterKey("id", descending: true)
                        .WithCompaction("TimeWindowCompactionStrategy",
                            ("compaction_window_unit", "DAYS"), ("compaction_window_size", "7")),
                    ifNotExists: true), ct);

        public Task Down(ScyllaDatabase db, CancellationToken ct = default)
            => db.ApplyMigrationAsync(m => m.DropTable(Table, ifExists: true), ct);

        public async Task<bool> Validate(ScyllaDatabase db, CancellationToken ct = default)
        {
            var rows = await db.ExecuteQueryAsync(
                "SELECT table_name FROM system_schema.tables WHERE keyspace_name = ? AND table_name = ?",
                [db.Keyspace, Table],
                ct);
            return rows.Any();
        }
    }
}
