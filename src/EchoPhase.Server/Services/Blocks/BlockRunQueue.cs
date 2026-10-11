// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Channels;
using EchoPhase.Configuration.Runner;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Services.Blocks
{
    /// <summary>
    /// Bounded hand-off between the API and <see cref="BlockRunWorker"/>. A full
    /// queue rejects new runs instead of letting requests pile up in memory.
    /// </summary>
    public sealed class BlockRunQueue
    {
        private readonly Channel<BlockRunJob> _channel;

        public BlockRunQueue(IOptions<RunnerOptions> options)
        {
            _channel = Channel.CreateBounded<BlockRunJob>(new BoundedChannelOptions(options.Value.Blocks.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            });
        }

        public bool TryEnqueue(BlockRunJob job) => _channel.Writer.TryWrite(job);

        public IAsyncEnumerable<BlockRunJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }
}
