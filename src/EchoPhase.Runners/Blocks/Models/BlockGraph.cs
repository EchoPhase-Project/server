// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>A validated program indexed for running: blocks by id, edges by (block, output).</summary>
    public sealed class BlockGraph
    {
        private static readonly IReadOnlyList<int> None = [];

        private readonly Dictionary<int, IBlock> _blocks;
        private readonly Dictionary<(int, string), List<int>> _targets = new();
        private readonly Dictionary<int, int> _incoming = new();

        public BlockGraph(IEnumerable<IBlock> blocks, IEnumerable<BlockConnection> connections)
        {
            _blocks = blocks.ToDictionary(b => b.Id);
            foreach (var c in connections)
            {
                if (!_targets.TryGetValue((c.From, c.Output), out var list))
                    _targets[(c.From, c.Output)] = list = [];
                list.Add(c.To);
                _incoming[c.To] = _incoming.GetValueOrDefault(c.To) + 1;
            }
        }

        public IEnumerable<IBlock> Blocks => _blocks.Values;

        public IBlock? Find(int id) => _blocks.GetValueOrDefault(id);

        /// <summary>Blocks connected to <paramref name="output"/> of <paramref name="id"/>, in declaration order.</summary>
        public IReadOnlyList<int> Targets(int id, string output)
            => _targets.TryGetValue((id, output), out var list) ? list : None;

        /// <summary>Edges into a block: a Merge waits for this many inputs.</summary>
        public int IncomingCount(int id) => _incoming.GetValueOrDefault(id);
    }
}
