// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Redis.Maintenance
{
    /// <summary>Maintenance mode while it is on: why, since when, and until when (null: until turned off).</summary>
    public sealed record MaintenanceState(string? Message, DateTimeOffset Since, DateTimeOffset? Until)
    {
        /// <summary>What clients are told to wait when no end is set.</summary>
        public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(2);

        /// <summary>How long a client should wait before trying again (Retry-After): at least a second.</summary>
        public TimeSpan RetryAfter(DateTimeOffset now)
            => Until is { } until
                ? TimeSpan.FromSeconds(Math.Max(1, Math.Ceiling((until - now).TotalSeconds)))
                : DefaultRetryAfter;

        public bool IsOver(DateTimeOffset now) => Until is { } until && until <= now;
    }
}
