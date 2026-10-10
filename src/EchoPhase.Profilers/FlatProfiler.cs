// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Text;
using EchoPhase.Profilers.Models;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Profilers
{
    /// <summary>One entry per step name, however it was reached.</summary>
    public class FlatProfiler : ProfilerBase
    {
        private readonly ConcurrentDictionary<string, ProfileNode> _nodes = new();

        public FlatProfiler(bool enabled = true, ILogger<FlatProfiler>? logger = null)
            : base(enabled, logger)
        {
        }

        public override void Clear() => _nodes.Clear();

        protected override IDisposable Begin(string name)
            => new StepTimer(_nodes.GetOrAdd(name, static n => new ProfileNode(n, parent: null)));

        protected override void DumpTo(StringBuilder sb)
        {
            foreach (var node in _nodes.Values.OrderByDescending(n => n.TotalMs))
                AppendStats(sb, node);
        }
    }
}
