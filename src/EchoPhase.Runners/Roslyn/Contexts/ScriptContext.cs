// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Discord;

namespace EchoPhase.Runners.Roslyn.Contexts
{
    public class ScriptContext : IScriptContext
    {
        public required IDiscordClient DiscordClient
        {
            set; get;
        }
    }
}
