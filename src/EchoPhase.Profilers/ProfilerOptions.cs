// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Profilers
{
    /// <summary>
    /// Off by default: every step costs a timestamp, an allocation count and a
    /// lock, on the scripting hot paths. Turn on to investigate, e.g.
    /// Profiling__Enabled=true; each scope (request, block run) logs its
    /// profile at Debug when it ends.
    /// </summary>
    public class ProfilerOptions
    {
        public const string SectionName = "Profiling";

        public bool Enabled { get; set; }

        public ProfilerMode Mode { get; set; } = ProfilerMode.Stack;
    }
}
