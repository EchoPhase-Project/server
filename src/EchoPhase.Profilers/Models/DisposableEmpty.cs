// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Profilers.Models
{
    internal class DisposableEmpty : IDisposable
    {
        public static readonly DisposableEmpty Instance = new();
        private DisposableEmpty()
        {
        }
        public void Dispose()
        {
        }
    }
}
