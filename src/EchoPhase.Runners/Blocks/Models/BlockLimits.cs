// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>Per-run limits enforced by <see cref="BlocksRunner"/>.</summary>
    public sealed record BlockLimits(int MaxSteps, int MaxOutputEntries)
    {
        public static readonly BlockLimits Unlimited = new(int.MaxValue, int.MaxValue);
    }
}
