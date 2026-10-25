// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>
    /// A conditional save did not apply: a concurrency token no longer held the
    /// value the entity was read with (someone saved it meanwhile), the row was
    /// gone, or an added one existed already. Like EF's
    /// DbUpdateConcurrencyException; the other entities of the save were written.
    /// </summary>
    public sealed class ScyllaConcurrencyException : Exception
    {
        public ScyllaConcurrencyException(IReadOnlyList<object> entities)
            : base($"{entities.Count} conditional write(s) did not apply: " +
                   string.Join(", ", entities.Select(e => e.GetType().Name)) +
                   ". The rows changed meanwhile (or exist, or are gone); read them again.")
        {
            Entities = entities;
        }

        /// <summary>The entities whose write did not apply; they stay tracked.</summary>
        public IReadOnlyList<object> Entities { get; }
    }
}
