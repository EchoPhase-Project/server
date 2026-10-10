// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    public class BlockHandlerResolver
    {
        private readonly BlockTypeRegistry _registry;

        public BlockHandlerResolver(BlockTypeRegistry registry)
        {
            _registry = registry;
        }

        public BlockTypeDescriptor Describe(IBlock block)
            => _registry.Find(block.Type, block.Version)
                ?? throw new InvalidOperationException($"Unknown block type {block.Type} v{block.Version}.");

        public IBlockHandler Get(IServiceProvider services, BlockTypeDescriptor descriptor)
            => (IBlockHandler)services.GetRequiredService(descriptor.HandlerType);
    }
}
