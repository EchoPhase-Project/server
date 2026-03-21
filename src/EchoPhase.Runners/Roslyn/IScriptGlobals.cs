// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Roslyn.Contexts;

namespace EchoPhase.Runners.Roslyn
{
    public interface IScriptGlobals<out T>
    {
        T Payload
        {
            get;
        }
        IScriptContext Context
        {
            get;
        }
    }
}
