// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.Controllers.Api.v1.Discord
{
    /// <param name="Name">The Discord bot connection to connect with.</param>
    /// <param name="Intents">Gateway intents bit set; default: guild and direct messages.</param>
    public sealed record GatewayConnectRequest(string Name, int? Intents = null);
}
