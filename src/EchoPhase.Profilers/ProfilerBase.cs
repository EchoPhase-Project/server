// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;
using System.Text;
using EchoPhase.Profilers.Models;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Profilers
{
    /// <summary>
    /// What both profilers share: the Step overloads, enabling, and the
    /// report. Disabled, a step allocates nothing. Disposed with its scope, an
    /// enabled profiler logs its report at Debug.
    /// </summary>
    public abstract class ProfilerBase : IProfiler, IDisposable
    {
        private readonly ILogger? _logger;
        private volatile bool _enabled;

        protected ProfilerBase(bool enabled, ILogger? logger = null)
        {
            _enabled = enabled;
            _logger = logger;
        }

        public bool IsEnabled => _enabled;

        public void Enable()
        {
            Clear();
            _enabled = true;
        }

        public void Disable() => _enabled = false;

        public abstract void Clear();

        public IDisposable Step([CallerMemberName] string memberName = "")
            => _enabled ? Begin(memberName) : DisposableEmpty.Instance;

        public void Step(Action action, [CallerMemberName] string memberName = "")
        {
            using (Step(memberName))
                action();
        }

        public T Step<T>(Func<T> action, [CallerMemberName] string memberName = "")
        {
            using (Step(memberName))
                return action();
        }

        public async Task StepAsync(Func<Task> func, [CallerMemberName] string memberName = "")
        {
            using (Step(memberName))
                await func();
        }

        public async Task<T> StepAsync<T>(Func<Task<T>> func, [CallerMemberName] string memberName = "")
        {
            using (Step(memberName))
                return await func();
        }

        public string Dump()
        {
            var sb = new StringBuilder();
            DumpTo(sb);
            return sb.ToString();
        }

        public void Dispose()
        {
            if (_enabled && _logger is not null && _logger.IsEnabled(LogLevel.Debug))
            {
                var report = Dump();
                if (report.Length > 0)
                    _logger.LogDebug("Profile:{NewLine}{Profile}", Environment.NewLine, report);
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>Starts timing a step; only called while enabled.</summary>
        protected abstract IDisposable Begin(string name);

        protected abstract void DumpTo(StringBuilder sb);

        protected static void AppendStats(StringBuilder sb, ProfileNode node)
            => sb.Append(node.Name)
                .Append(": count=").Append(node.Count)
                .Append(", avg=").Append(node.AverageMs.ToString("F3")).Append(" ms")
                .Append(", min=").Append(node.MinMs.ToString("F3")).Append(" ms")
                .Append(", max=").Append(node.MaxMs.ToString("F3")).Append(" ms")
                .Append(", total=").Append(node.TotalMs.ToString("F3")).Append(" ms")
                .Append(", allocated=").AppendLine(FormatBytes(node.AllocatedBytes));

        private static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB"];
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return order == 0 ? $"{bytes} B" : $"{len:F2} {sizes[order]}";
        }
    }
}
