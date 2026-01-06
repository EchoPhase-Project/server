using System.Runtime.CompilerServices;
using System.Text;
using EchoPhase.Analytics.Models;
using EchoPhase.Interfaces;

namespace EchoPhase.Analytics
{
    public class FlatProfiler : IProfiler
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, ProfileNode> _callerTimings = new();

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
                _callerTimings.Clear();
            }
        }

        public IDisposable Step([CallerMemberName] string memberName = "")
        {
            if (!IsEnabled)
                return DisposableEmpty.Instance;

            var node = GetOrCreateNode(memberName);
            return new StepTimer(node);
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
                foreach (var kvp in _callerTimings)
                {
                    DumpNodeStats(sb, kvp.Value, kvp.Key);
                }
            }
            return sb.ToString();
        }

        private ProfileNode GetOrCreateNode(string name)
        {
            lock (_lock)
            {
                if (!_callerTimings.TryGetValue(name, out var node))
                    _callerTimings[name] = node = new ProfileNode(name);

                return node;
            }
        }

        private void DumpNodeStats(StringBuilder sb, ProfileNode node, string label)
        {
            sb.AppendLine($"{label}: count={node.Count}, avg={node.AverageMs:F3} ms, min={node.MinMs:F3} ms, max={node.MaxMs:F3} ms, total={node.TotalMs:F3} ms, memoryChange={FormatBytes(node.TotalMemoryChangeBytes)}");
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
