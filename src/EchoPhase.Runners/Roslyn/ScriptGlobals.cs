// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Roslyn.Contexts;

namespace EchoPhase.Runners.Roslyn
{
    public class ScriptGlobals<T> : IScriptGlobals<T>
    {
        public required T Payload
        {
            set; get;
        }
        public required IScriptContext Context
        {
            set; get;
        }
    }
}
