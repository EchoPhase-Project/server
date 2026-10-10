// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Observers;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Runners.Blocks
{
    /// <summary>
    /// Runs a program: each block's output goes to the blocks connected to
    /// the output it finished through, with that data as their input. A
    /// Merge waits for all its inputs (or, when the rest never come, runs with
    /// those that did once nothing else is left). Loops run their body as a
    /// nested branch. Programs are validated (no cycles) before they get here;
    /// MaxSteps bounds whatever slips through.
    /// </summary>
    public class BlocksRunner
    {
        /// <summary>Input source of the first blocks: none.</summary>
        public const int NoSource = -1;

        private readonly BlockHandlerResolver _resolver;
        private readonly IBlockRunObserver[] _observers;
        private readonly TimeProvider _time;
        private readonly ILogger<BlocksRunner> _logger;

        public BlocksRunner(
            BlockHandlerResolver resolver,
            IEnumerable<IBlockRunObserver> observers,
            TimeProvider time,
            ILogger<BlocksRunner> logger)
        {
            _resolver = resolver;
            _observers = observers.ToArray();
            _time = time;
            _logger = logger;
        }

        /// <summary>
        /// Runs from <paramref name="startIds"/>, each getting <paramref name="input"/>
        /// (a trigger's payload). A limit violation or a failure with the Stop
        /// policy ends the run; both are in the context's errors.
        /// </summary>
        public async Task<IBlockExecutionContext> RunAsync(
            IBlockExecutionContext context, IEnumerable<int> startIds, object? input = null)
        {
            try
            {
                await ExecuteAsync(context, startIds.Select(id => (id, new BlockInput(NoSource, input))));
            }
            catch (BlockLimitExceededException ex)
            {
                var error = new BlockError { Id = ex.BlockId, Type = "LimitExceeded", Message = ex.Message };
                context.Errors.Add(error);
                if (context.Graph.Find(ex.BlockId) is { } block)
                    await NotifyAsync(o => o.OnBlockFailedAsync(
                        new BlockFailedEvent(context, block, [], context.Depth, error, 1, false, false)));
            }
            catch (BlockRunStoppedException)
            {
                // Recorded where it happened.
            }

            return context;
        }

        /// <summary>
        /// Counts one step against <see cref="BlockLimits.MaxSteps"/>. Loop handlers
        /// call it per iteration, so even a loop with an empty body is bounded.
        /// </summary>
        public static void CountStep(IBlockExecutionContext context, int blockId)
        {
            if (++context.Steps > context.Limits.MaxSteps)
                throw new BlockLimitExceededException(blockId,
                    $"Run exceeded {context.Limits.MaxSteps} steps.");
        }

        private async Task ExecuteAsync(IBlockExecutionContext context, IEnumerable<(int Id, BlockInput Input)> starts)
        {
            var queue = new Queue<(int Id, BlockInput Input)>(starts);
            var waiting = new SortedDictionary<int, List<BlockInput>>();

            int depth = context.Depth;
            context.Depth = depth + 1;
            try
            {
                while (true)
                {
                    while (queue.TryDequeue(out var item))
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();

                        if (context.Graph.Find(item.Id) is not { } block)
                        {
                            // Validation rejects such programs; keep running the rest.
                            context.Errors.Add(new BlockError
                            {
                                Id = item.Id,
                                Type = "UnknownBlock",
                                Message = $"Block {item.Id} does not exist."
                            });
                            continue;
                        }

                        if (block.Type == BlockTypes.Merge && !block.Disabled)
                        {
                            if (!waiting.TryGetValue(block.Id, out var arrived))
                                waiting[block.Id] = arrived = [];
                            arrived.Add(item.Input);
                            if (arrived.Count < context.Graph.IncomingCount(block.Id))
                                continue;

                            waiting.Remove(block.Id);
                            await RunBlockAsync(context, block, arrived, depth, queue);
                        }
                        else
                        {
                            await RunBlockAsync(context, block, [item.Input], depth, queue);
                        }
                    }

                    // Inputs that will never come (an untaken branch): run the
                    // first waiting Merge with what it got, then carry on.
                    if (waiting.Count == 0)
                        break;
                    var (mergeId, inputs) = waiting.First();
                    waiting.Remove(mergeId);
                    await RunBlockAsync(context, context.Graph.Find(mergeId)!, inputs, depth, queue);
                }
            }
            finally
            {
                context.Depth = depth;
            }
        }

        private async Task RunBlockAsync(
            IBlockExecutionContext context, IBlock block, IReadOnlyList<BlockInput> inputs, int depth,
            Queue<(int Id, BlockInput Input)> queue)
        {
            CountStep(context, block.Id);

            var descriptor = _resolver.Describe(block);
            var call = new BlockCall(context, block, inputs, RunBranchAsync);

            if (_observers.Length > 0)
                await NotifyAsync(o => o.OnBlockStartingAsync(new BlockEvent(context, block, inputs, depth)));

            long started = _time.GetTimestamp();
            var result = block.Disabled
                ? new BlockResult(descriptor.FirstOutput, call.Input)
                : await InvokeAsync(call, descriptor, depth);

            context.BlockOutputs[BlockExecutionContext.OutputKey(block.Id)] = result.Data;

            if (_observers.Length > 0)
            {
                var elapsed = _time.GetElapsedTime(started);
                await NotifyAsync(o => o.OnBlockCompletedAsync(new BlockCompletedEvent(
                    context, block, inputs, depth, result.Output, result.Data, elapsed, block.Disabled)));
            }

            if (context.Output.Count > context.Limits.MaxOutputEntries)
                throw new BlockLimitExceededException(block.Id,
                    $"Output exceeded {context.Limits.MaxOutputEntries} entries.");

            if (result.Output is { } output)
                foreach (var target in context.Graph.Targets(block.Id, output))
                    queue.Enqueue((target, new BlockInput(block.Id, result.Data)));
        }

        // A loop's body (or any branch a handler runs itself), one level deeper.
        private Task RunBranchAsync(BlockCall call, string output, object? input)
            => ExecuteAsync(call.Context, call.Context.Graph.Targets(call.Block.Id, output)
                .Select(target => (target, new BlockInput(call.Block.Id, input))));

        private async Task<BlockResult> InvokeAsync(BlockCall call, BlockTypeDescriptor descriptor, int depth)
        {
            var context = call.Context;
            var block = call.Block;
            int tries = Math.Clamp(block.Retry?.MaxTries ?? 1, 1, 10);

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    var handler = _resolver.Get(context.Services, descriptor);
                    return await handler.ExecuteAsync(call, handler.Bind(block.Params));
                }
                // Cancellation, limits and a stop elsewhere are not this block's failure.
                catch (Exception ex) when (ex is not OperationCanceledException
                                               and not BlockLimitExceededException
                                               and not BlockRunStoppedException)
                {
                    var error = new BlockError { Id = block.Id, Type = ex.GetType().Name, Message = ex.Message };
                    bool retry = attempt < tries;
                    bool handled = !retry && block.OnError != BlockErrorPolicy.Stop;

                    if (!retry && !handled)
                        context.Errors.Add(error);

                    if (_observers.Length > 0)
                        await NotifyAsync(o => o.OnBlockFailedAsync(new BlockFailedEvent(
                            context, block, call.Inputs, depth, error, attempt, retry, handled)));

                    if (retry)
                    {
                        var delay = TimeSpan.FromMilliseconds(Math.Clamp(block.Retry!.DelayMs, 0, 60_000));
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, _time, context.CancellationToken);
                        continue;
                    }

                    var data = ErrorData(error);
                    return block.OnError switch
                    {
                        BlockErrorPolicy.Continue => new BlockResult(descriptor.FirstOutput, data),
                        BlockErrorPolicy.ErrorOutput => new BlockResult(BlockOutputs.Error, data),
                        _ => throw new BlockRunStoppedException(block.Id)
                    };
                }
            }
        }

        // What a handled failure looks like as output: blocks.b3.error.message.
        private static Dictionary<string, object?> ErrorData(BlockError error) => new()
        {
            ["error"] = new Dictionary<string, object?>
            {
                ["type"] = error.Type,
                ["message"] = error.Message
            }
        };

        // Observers watch; they never decide the run's outcome.
        private async Task NotifyAsync(Func<IBlockRunObserver, ValueTask> notify)
        {
            foreach (var observer in _observers)
            {
                try
                {
                    await notify(observer);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Block run observer {Observer} failed.", observer.GetType().Name);
                }
            }
        }
    }
}
