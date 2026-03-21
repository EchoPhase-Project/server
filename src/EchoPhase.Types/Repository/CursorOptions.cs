// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Repository
{
    public class CursorOptions
    {
        public string? After { get; set; } = null;
        public int Limit { get; set; } = 20;
    }
}
