// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Models
{
    /// <summary>
    /// Per-write <c>USING</c> clause.
    /// </summary>
    /// <param name="Ttl">
    /// The written values expire after this long (overrides the table's
    /// default_time_to_live). Not valid for deletes.
    /// </param>
    /// <param name="Timestamp">
    /// Write timestamp used for last-write-wins conflict resolution; defaults to
    /// the coordinator's clock.
    /// </param>
    /// <param name="Statement">Consistency (and LWT serial consistency) and timeout of the write.</param>
    public sealed record WriteOptions(TimeSpan? Ttl = null, DateTimeOffset? Timestamp = null,
        Database.StatementOptions? Statement = null)
    {
        /// <summary>No USING clause (statement options are not part of the CQL text).</summary>
        internal bool IsEmpty => Ttl is null && Timestamp is null;

        internal void Validate()
        {
            if (Ttl is { } ttl && (ttl <= TimeSpan.Zero || ttl.TotalSeconds > int.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(Ttl), "TTL must be positive and fit in an int of seconds.");
        }

        internal int? TtlSeconds => Ttl is { } ttl ? (int)ttl.TotalSeconds : null;

        // CQL timestamps are microseconds since the Unix epoch.
        internal long? TimestampMicros => Timestamp is { } ts ? (ts - DateTimeOffset.UnixEpoch).Ticks / 10 : null;
    }
}
