// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Parsed sources, bounded and approximately least-recently-used: past
    /// the capacity, the quarter used longest ago goes, so the expressions
    /// programs run all the time stay parsed however many others come and go
    /// (a server holds many users' workflows). A hit only reads a clock and,
    /// at most once per millisecond, writes the entry's own field: threads do
    /// not contend on a shared counter. Failed parses are not kept.
    /// </summary>
    internal sealed class ParseCache<T>(int capacity, Func<string, T> parse, Func<long>? clock = null) where T : class
    {
        private readonly Func<long> _clock = clock ?? (static () => Environment.TickCount64);

        private sealed class Entry(T value, long used)
        {
            public readonly T Value = value;
            public long LastUsed = used;
        }

        private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly Lock _evicting = new();
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public T Get(string source)
        {
            var now = _clock();
            if (_entries.TryGetValue(source, out var hit))
            {
                if (hit.LastUsed != now)
                    hit.LastUsed = now;
                return hit.Value;
            }

            var parsed = parse(source);
            if (_entries.TryAdd(source, new Entry(parsed, now)) && Interlocked.Increment(ref _count) > capacity)
                Evict();
            return parsed;
        }

        // One thread trims at a time; the others carry on reading and adding.
        // An add while it trims is either seen by its re-check after unlocking
        // (the add counted before that) or trims on its own (lock free again),
        // so the cache never stays over its capacity.
        private void Evict()
        {
            while (Count > capacity)
            {
                if (!_evicting.TryEnter())
                    return;
                try
                {
                    int excess = Count - capacity * 3 / 4;
                    if (excess <= 0)
                        continue;

                    // ToArray is the dictionary's own atomic snapshot; LINQ would copy by
                    // a count read earlier and fail when another thread adds meanwhile.
                    // Hits keep moving LastUsed, so it is read once into keys of its own:
                    // a sort comparing live values can see them change and throw.
                    var entries = _entries.ToArray();
                    var used = new long[entries.Length];
                    for (int i = 0; i < entries.Length; i++)
                        used[i] = Volatile.Read(ref entries[i].Value.LastUsed);
                    Array.Sort(used, entries);
                    foreach (var (key, _) in entries.AsSpan(0, Math.Min(excess, entries.Length)))
                        if (_entries.TryRemove(key, out _))
                            Interlocked.Decrement(ref _count);
                }
                finally
                {
                    _evicting.Exit();
                }
            }
        }
    }
}
