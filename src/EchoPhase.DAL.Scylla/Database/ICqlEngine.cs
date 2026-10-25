// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Cassandra;

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>One CQL statement with its bound values.</summary>
    /// <param name="Cql">The text, with ? markers.</param>
    /// <param name="Values">One per marker, in order.</param>
    /// <param name="Idempotent">Safe to run twice (no LWT, no counters): the driver retries only these.</param>
    /// <param name="PageSize">One page of this many rows; null: every row.</param>
    /// <param name="PagingState">Where the page starts (from the previous result); null: the first.</param>
    public sealed record CqlStatement(
        string Cql,
        object?[] Values,
        bool Idempotent = false,
        int? PageSize = null,
        byte[]? PagingState = null);

    /// <summary>What a statement returned: its rows, and the cursor to the next page, if any.</summary>
    public sealed record CqlResult(IReadOnlyList<Row> Rows, byte[]? PagingState)
    {
        public static readonly CqlResult Empty = new([], null);

        /// <summary>
        /// For a lightweight transaction (IF …): whether it applied. Null for
        /// other statements.
        /// </summary>
        public bool? Applied => Rows.Count > 0 && Rows[0].GetColumn("[applied]") is not null
            ? Rows[0].GetValue<bool>("[applied]")
            : null;
    }

    /// <summary>
    /// Runs CQL: the cluster (through the driver) or an in-memory stand-in for
    /// tests. Everything the ORM sends goes through here.
    /// </summary>
    public interface ICqlEngine : IDisposable
    {
        CqlResult Execute(CqlStatement statement);

        Task<CqlResult> ExecuteAsync(CqlStatement statement, CancellationToken ct = default);

        /// <summary>Statements sent together (not a transaction; see <see cref="Batch"/>).</summary>
        void ExecuteBatch(IReadOnlyList<CqlStatement> statements, BatchType type);

        Task ExecuteBatchAsync(IReadOnlyList<CqlStatement> statements, BatchType type, CancellationToken ct = default);
    }
}
