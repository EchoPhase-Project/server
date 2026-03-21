// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks
{
    public class BlocksRunner
    {
        private readonly BlockHandlerResolver _resolver;

        public BlocksRunner(BlockHandlerResolver resolver)
        {
            _resolver = resolver;
        }

        public static IBlockExecutionContext Create(
            IEnumerable<int>? startIds = null,
            IEnumerable<IBlock>? blocks = null)
        {
            return new BlockExecutionContext
            {
                StartIds = startIds?.ToList() ?? new List<int>(),
                Blocks = blocks?.ToList() ?? new List<IBlock>(),
            };
        }

        public async Task<IBlockExecutionContext> ExecuteAsync(IBlockExecutionContext context)
        {
            context ??= new BlockExecutionContext();

            var dict = context.Blocks.ToDictionary(b => b.Id);
            var queue = new Queue<int>();
            var visited = new HashSet<int>();

            foreach (var startId in context.StartIds)
                queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!dict.TryGetValue(id, out var block) || !visited.Add(id))
                {
                    var error = new BlockError
                    {
                        Id = id,
                        Type = "InfiniteLoopException",
                        Message = $"Block with {id} was already executed."
                    };

                    context.Errors.Add(error);
                    continue;
                }

                var handler = _resolver.Get(block.Type);

                IEnumerable<int> next = new HashSet<int>();
                try
                {
                    next = await handler.ExecuteAsync(context, block, block.Params);
                }
                catch (Exception ex)
                {
                    var error = new BlockError
                    {
                        Id = id,
                        Type = ex.GetType().Name,
                        Message = ex.Message
                    };

                    context.Errors.Add(error);
                    throw;
                }

                foreach (var nextId in next)
                    queue.Enqueue(nextId);
            }

            return context;
        }
    }
}
