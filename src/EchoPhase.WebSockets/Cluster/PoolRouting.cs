// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IO.Hashing;
using System.Text;

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>
    /// Which member of a pool gets an event: rendezvous hashing of the
    /// event's key with each member. The same key goes to the same member
    /// while it is there; when one leaves, only its keys move (to the next
    /// best), and a member joining takes a fair share of keys from the rest.
    /// </summary>
    public static class PoolRouting
    {
        /// <summary>The member for <paramref name="key"/>, or null when none is left.</summary>
        public static PoolMember? Pick(IEnumerable<PoolMember> members, string key, IReadOnlySet<string>? exclude = null)
        {
            PoolMember? best = null;
            ulong bestScore = 0;
            foreach (var member in members)
            {
                if (exclude?.Contains(member.Id) == true)
                    continue;
                var score = Score(key, member.Id);
                // Ties (practically never) broken by id, so every replica agrees.
                if (best is null || score > bestScore
                    || (score == bestScore && string.CompareOrdinal(member.Id, best.Id) < 0))
                    (best, bestScore) = (member, score);
            }
            return best;
        }

        public static ulong Score(string key, string memberId)
            => XxHash3.HashToUInt64(Encoding.UTF8.GetBytes($"{key}\n{memberId}"));
    }
}
