using System.Runtime.CompilerServices;
using System.Text;
using EchoPhase.Profilers.Models;

namespace EchoPhase.Profilers
{
    public class StackProfiler : IProfiler
    {
        private readonly object _lock = new();
        private readonly List<ProfileNode> _stackRoots = new();
        private readonly Stack<ProfileNode> _stack = new();

        public bool IsEnabled { get; private set; } = true;

        public void Enable()
        {
            Clear();
            IsEnabled = true;
        }

        public void Disable() => IsEnabled = false;

        public void Clear()
        {
            lock (_lock)
            {
                _stack.Clear();
                _stackRoots.Clear();
            }
        }

        private ProfileNode FindOrCreateNode(string baseName, List<ProfileNode> siblings, Stack<ProfileNode> currentStack)
        {
            var uniqueName = baseName;
            int suffix = 1;

            while (currentStack.Any(n => n.Name == uniqueName))
            {
                uniqueName = $"{baseName}#{suffix++}";
            }

            var existing = siblings.FirstOrDefault(n => n.Name == uniqueName);
            if (existing != null)
                return existing;

            var newNode = new ProfileNode(uniqueName);
            siblings.Add(newNode);
            return newNode;
        }

        public IDisposable Step([CallerMemberName] string memberName = "")
        {
            if (!IsEnabled)
                return DisposableEmpty.Instance;

            ProfileNode node;

            lock (_lock)
            {
                if (_stack.Count == 0)
                {
                    node = FindOrCreateNode(memberName, _stackRoots, _stack);
                }
                else
                {
                    var parent = _stack.Peek();
                    node = FindOrCreateNode(memberName, parent.Children, _stack);
                }

                _stack.Push(node);
            }

            return new StepTimer(node, () =>
            {
                lock (_lock)
                {
                    if (_stack.Count == 0 || _stack.Peek() != node)
                        throw new InvalidOperationException("Profiler stack corrupted");
                    _stack.Pop();
                }
            });
        }

        public void Step(Action action, [CallerMemberName] string memberName = "")
        {
            using (Step(memberName))
            {
                action();
            }
        }

        public T Step<T>(Func<T> action, [CallerMemberName] string memberName = "")
        {
            using (Step(memberName))
            {
                return action();
            }
        }

        public async Task StepAsync(Func<Task> func, [CallerMemberName] string memberName = "")
        {
            if (!IsEnabled)
            {
                await func();
                return;
            }

            using (Step(memberName))
            {
                await func();
            }
        }

        public async Task<T> StepAsync<T>(Func<Task<T>> func, [CallerMemberName] string memberName = "")
        {
            if (!IsEnabled)
                return await func();

            using (Step(memberName))
            {
                return await func();
            }
        }

        public string Dump()
        {
            var sb = new StringBuilder();
            lock (_lock)
            {
                foreach (var root in _stackRoots)
                    DumpTree(sb, root, level: 0, isLasts: new List<bool>());
            }
            return sb.ToString();
        }

        private void DumpTree(StringBuilder sb, ProfileNode node, int level, List<bool> isLasts)
        {
            for (int i = 0; i < level; i++)
            {
                if (i == level - 1)
                {
                    sb.Append(isLasts[i] ? "└─" : "├─");
                }
                else
                {
                    sb.Append(isLasts[i] ? "  " : "│ ");
                }
            }

            sb.AppendLine($"{node.Name}: count={node.Count}, avg={node.AverageMs:F3} ms, min={node.MinMs:F3} ms, max={node.MaxMs:F3} ms, total={node.TotalMs:F3} ms, memoryChange={FormatBytes(node.TotalMemoryChangeBytes)}");

            for (int i = 0; i < node.Children.Count; i++)
            {
                bool isLast = (i == node.Children.Count - 1);
                var newIsLasts = new List<bool>(isLasts) { isLast };
                DumpTree(sb, node.Children[i], level + 1, newIsLasts);
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }
    }
}
