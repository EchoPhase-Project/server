// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    public class BlockHandlerResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public BlockHandlerResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IBlockHandler Get(BlockTypes type)
        {
            return _serviceProvider.GetKeyedService<IBlockHandler>(type)
                ?? throw new InvalidOperationException($"No handler for block type: {type}");
        }
    }
}
