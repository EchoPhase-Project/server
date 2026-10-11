// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.Services.Health
{
    /// <summary>Set by <see cref="WarmupService"/> once the first request will be fast.</summary>
    public sealed class WarmupState
    {
        private volatile bool _ready;

        public bool IsReady => _ready;

        public void MarkReady() => _ready = true;
    }
}
