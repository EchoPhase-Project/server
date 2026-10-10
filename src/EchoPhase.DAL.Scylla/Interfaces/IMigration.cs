// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Interfaces
{
    /// <summary>
    /// A schema change. CQL DDL is not transactional: if <see cref="Up"/> fails
    /// halfway, the statements already run stay applied, so <see cref="Down"/>
    /// must cope with a partially applied <see cref="Up"/> (use IF EXISTS).
    /// </summary>
    public interface IMigration
    {
        /// <summary>Sortable, unique id, e.g. <c>20261008_create_audit</c>.</summary>
        string Id
        {
            get;
        }

        Task Up(ScyllaDatabase db, CancellationToken ct = default);
        Task Down(ScyllaDatabase db, CancellationToken ct = default);
        Task<bool> Validate(ScyllaDatabase db, CancellationToken ct = default);
    }
}
