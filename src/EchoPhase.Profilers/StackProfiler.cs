// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using EchoPhase.Profilers.Models;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Profilers
{
    /// <summary>
    /// A tree of steps by caller. The current step is tracked per async flow
    /// (AsyncLocal), so steps of concurrent tasks in one scope nest under
    /// their own callers instead of corrupting a shared stack.
    /// </summary>
    public class StackProfiler : ProfilerBase
    {
        private readonly ProfileNode _root = new("", parent: null);
        private readonly AsyncLocal<ProfileNode?> _current = new();

        public StackProfiler(bool enabled = true, ILogger<StackProfiler>? logger = null)
            : base(enabled, logger)
        {
        }

        public override void Clear()
        {
            _root.ClearChildren();
            _current.Value = null;
        }

        protected override IDisposable Begin(string name)
        {
            var parent = _current.Value ?? _root;
            var node = parent.GetOrAddChild(UniqueInChain(name, parent));
            _current.Value = node;

            // Restoring the parent (not popping) keeps each async flow's
            // position its own: ExecutionContext gives every flow a copy.
            return new StepTimer(node, () => _current.Value = node.Parent == _root ? null : node.Parent);
        }

        // A recursive step gets its own level: name, name#1, name#2...
        private static string UniqueInChain(string name, ProfileNode parent)
        {
            var unique = name;
            for (int suffix = 1; InChain(unique, parent); suffix++)
                unique = $"{name}#{suffix}";
            return unique;
        }

        private static bool InChain(string name, ProfileNode? node)
        {
            for (; node is not null; node = node.Parent)
                if (node.Name == name)
                    return true;
            return false;
        }

        protected override void DumpTo(StringBuilder sb)
        {
            var roots = _root.SnapshotChildren();
            for (int i = 0; i < roots.Count; i++)
                DumpTree(sb, roots[i], prefix: "", isRoot: true, isLast: i == roots.Count - 1);
        }

        private static void DumpTree(StringBuilder sb, ProfileNode node, string prefix, bool isRoot, bool isLast)
        {
            sb.Append(prefix);
            if (!isRoot)
                sb.Append(isLast ? "└─" : "├─");
            AppendStats(sb, node);

            var childPrefix = isRoot ? "" : prefix + (isLast ? "  " : "│ ");
            var children = node.SnapshotChildren();
            for (int i = 0; i < children.Count; i++)
                DumpTree(sb, children[i], childPrefix, isRoot: false, isLast: i == children.Count - 1);
        }
    }
}
