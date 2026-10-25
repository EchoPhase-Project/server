// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Models
{
    /// <summary>One check of an IF clause: a property, an operator (=, !=, &lt;, &lt;=, &gt;, &gt;=) and a value.</summary>
    public sealed record ConditionCheck(string Property, string Operator, object? Value);

    /// <summary>
    /// What a write checks first, as a lightweight transaction (Paxos on the
    /// partition): the row exists, does not, or holds certain values. Costs
    /// several round trips: for writes that must not race, not for every write.
    /// </summary>
    public sealed record WriteCondition(bool IfExists, bool IfNotExists, IReadOnlyList<ConditionCheck> Checks)
    {
        public static WriteCondition Exists { get; } = new(true, false, []);

        public static WriteCondition NotExists { get; } = new(false, true, []);

        public static WriteCondition Where(params ConditionCheck[] checks) => new(false, false, checks);
    }
}
