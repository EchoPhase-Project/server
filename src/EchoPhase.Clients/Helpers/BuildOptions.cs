// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Helpers
{
    public class BuildOptions : IBuildOptions
    {
        public bool IncludeProperties { get; set; } = true;
        public bool IncludeFields { get; set; } = false;
    }
}
