// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    public interface IOpCodeHandler
    {
        Task HandleAsync(WebSocket socket, object payload);
    }

    public interface IOpCodeHandler<TPayload> : IOpCodeHandler
    {
        Task HandleAsync(WebSocket socket, TPayload payload);
    }
}
