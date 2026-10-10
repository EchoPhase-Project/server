// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Profilers
{
    public enum ProfilerMode
    {
        /// <summary>One line per step name, however it was reached.</summary>
        Flat,

        /// <summary>A tree of steps by where they were called from.</summary>
        Stack
    }
}
