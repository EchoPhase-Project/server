// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Params;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>
    /// Joins branches: runs once all its inputs arrived, or, when some never
    /// will (an untaken If branch), with those that did once nothing else is left.
    /// </summary>
    [BlockType(BlockTypes.Merge, Description = "Waits for its inputs and joins them: one list (Append) or one object by source (BySource).")]
    public class MergeHandler : BlockHandlerBase<MergeParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, MergeParams param)
        {
            object data = param.Mode switch
            {
                MergeMode.BySource => call.Inputs
                    .GroupBy(i => i.From)
                    .ToDictionary(g => BlockExecutionContext.OutputKey(g.Key), g => g.Last().Data),
                _ => Append(call.Inputs)
            };

            return Task.FromResult(BlockResult.Main(data));
        }

        private static List<object?> Append(IEnumerable<BlockInput> inputs)
        {
            var list = new List<object?>();
            foreach (var input in inputs)
            {
                if (input.Data is IList items)
                    list.AddRange(items.Cast<object?>());
                else if (input.Data is not null)
                    list.Add(input.Data);
            }
            return list;
        }
    }
}
