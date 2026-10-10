// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    public interface IOpCodeHandler
    {
        /// <summary>Handles one client message; <paramref name="payload"/> is its "d".</summary>
        Task HandleAsync(WebSocketConnection connection, JsonElement payload, CancellationToken ct = default);
    }
}
