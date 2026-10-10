// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Observers
{
    public enum BlockExecutionStatus
    {
        Succeeded,
        /// <summary>Failed, and the block's policy turned the error into output.</summary>
        Handled,
        Failed,
        Disabled
    }

    /// <summary>One execution of one block, as stored with the run.</summary>
    public sealed record BlockExecutionRecord(
        int BlockId,
        string Type,
        int Depth,
        BlockExecutionStatus Status,
        string? Output,
        double DurationMs,
        int Attempts,
        string? Data,
        BlockError? Error);

    /// <summary>
    /// Keeps what each block did (status, output followed, time, tries and a
    /// clipped copy of its data) for the run's execution data: what n8n shows
    /// on each node after a run. One per run; bounded in count and size.
    /// </summary>
    public sealed class ExecutionRecorder : IBlockRunObserver
    {
        public const int MaxRecords = 500;
        public const int MaxDataChars = 2048;

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly List<BlockExecutionRecord> _records = [];
        private readonly Dictionary<int, (int Attempts, BlockError? Handled)> _failures = new();

        public IReadOnlyList<BlockExecutionRecord> Records => _records;

        /// <summary>Executions not kept once <see cref="MaxRecords"/> was reached.</summary>
        public int Dropped { get; private set; }

        public ValueTask OnBlockFailedAsync(BlockFailedEvent e)
        {
            if (e.WillRetry || e.Handled)
            {
                _failures[e.Block.Id] = (e.Attempt, e.Handled ? e.Error : null);
            }
            else
            {
                // A stopping failure has no completion: record it here.
                _failures.Remove(e.Block.Id);
                Add(new BlockExecutionRecord(e.Block.Id, e.Block.Type.ToString(), e.Depth,
                    BlockExecutionStatus.Failed, null, 0, e.Attempt, null, e.Error));
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnBlockCompletedAsync(BlockCompletedEvent e)
        {
            // Failed tries before this completion: a handled failure ended on
            // its last try; a success came one try after the last failure.
            int attempts = 1;
            BlockError? handled = null;
            if (_failures.Remove(e.Block.Id, out var failure))
            {
                handled = failure.Handled;
                attempts = handled is not null ? failure.Attempts : failure.Attempts + 1;
            }

            var status = e.Disabled ? BlockExecutionStatus.Disabled
                : handled is not null ? BlockExecutionStatus.Handled
                : BlockExecutionStatus.Succeeded;

            Add(new BlockExecutionRecord(e.Block.Id, e.Block.Type.ToString(), e.Depth, status, e.Output,
                e.Elapsed.TotalMilliseconds, attempts, Clip(e.Data), handled));
            return ValueTask.CompletedTask;
        }

        private void Add(BlockExecutionRecord record)
        {
            if (_records.Count < MaxRecords)
                _records.Add(record);
            else
                Dropped++;
        }

        private static string? Clip(object? data)
        {
            if (data is null)
                return null;
            try
            {
                var json = JsonSerializer.Serialize(data, Json);
                return json.Length <= MaxDataChars ? json : json[..MaxDataChars] + "…";
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                return "(not serializable)";
            }
        }
    }
}
