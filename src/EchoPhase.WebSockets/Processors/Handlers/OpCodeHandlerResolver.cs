// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    public class OpCodeHandlerResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public OpCodeHandlerResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IOpCodeHandler GetHandler(OpCodes opCode)
        {
            if (!Enum.IsDefined(typeof(OpCodes), opCode))
                throw new NotSupportedException($"Unsupported OpCode {opCode} value.");

            if (opCode.IsIgnored())
                throw new NotSupportedException($"Ignored OpCode {opCode} value.");

            return _serviceProvider.GetKeyedService<IOpCodeHandler>(opCode)
                ?? throw new NotSupportedException($"No handler registered for OpCode {opCode}.");
        }

        public async Task HandleAsync(OpCodes opCode, WebSocket webSocket, object payload)
        {
            var handler = GetHandler(opCode);
            await handler.HandleAsync(webSocket, payload);
        }
    }
}
