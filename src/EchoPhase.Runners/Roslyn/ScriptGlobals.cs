// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Roslyn.Contexts;

namespace EchoPhase.Runners.Roslyn
{
    /// <summary>
    /// Globals object passed to Roslyn scripts.
    /// </summary>
    public class ScriptGlobals<T>
    {
        public required T Payload { get; set; }
        public required IScriptContext Context { get; set; }
    }
}
