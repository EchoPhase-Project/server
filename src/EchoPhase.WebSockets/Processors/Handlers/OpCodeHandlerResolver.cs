// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    /// <summary>Handlers by op code, from the connection's scope (they may use scoped services).</summary>
    public class OpCodeHandlerResolver
    {
        private readonly IServiceProvider _services;

        public OpCodeHandlerResolver(IServiceProvider services)
        {
            _services = services;
        }

        public IOpCodeHandler? Find(OpCodes opCode) => _services.GetKeyedService<IOpCodeHandler>(opCode);
    }
}
