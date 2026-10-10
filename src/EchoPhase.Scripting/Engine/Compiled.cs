// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Collections.Concurrent;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Parse once, evaluate many times: programs evaluate the same
    /// conditions and texts on every loop iteration and every run. Bounded:
    /// past <see cref="Capacity"/> entries a cache starts over.
    /// </summary>
    internal static class Compiled
    {
        public const int Capacity = 4096;

        private static readonly ConcurrentDictionary<string, Node> Expressions = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, Template> Templates = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, PathNode> Paths = new(StringComparer.Ordinal);

        public static Node Expression(string source) => Get(Expressions, source, ExpressionParser.Parse);

        public static Template Template(string source) => Get(Templates, source, Engine.Template.Parse);

        public static PathNode Path(string source) => Get(Paths, source, ExpressionParser.ParsePath);

        private static T Get<T>(ConcurrentDictionary<string, T> cache, string source, Func<string, T> parse)
        {
            if (cache.TryGetValue(source, out var hit))
                return hit;

            var parsed = parse(source);
            if (cache.Count >= Capacity)
                cache.Clear();
            cache[source] = parsed;
            return parsed;
        }

        /// <summary>
        /// Assigns <paramref name="value"/> at a path, creating the
        /// dictionaries and lists on the way: <c>a.b[2].c</c> on empty
        /// variables makes a = { b: [null, null, { c: value }] }.
        /// </summary>
        public static void Assign(PathNode path, IDictionary<string, object> variables, object? value)
        {
            if (path.Segments.Count == 0)
            {
                variables[path.Root] = value!;
                return;
            }

            if (!variables.TryGetValue(path.Root, out var current) || current is null)
                variables[path.Root] = current = NewContainer(path.Segments[0]);

            for (int i = 0; i < path.Segments.Count; i++)
            {
                var segment = path.Segments[i];
                bool last = i == path.Segments.Count - 1;
                object? key = segment is IndexSegment ix ? Values.Unwrap(ix.Index.Evaluate(variables)) : ((MemberSegment)segment).Name;

                var child = last ? value : Read(current, key) ?? NewContainer(path.Segments[i + 1]);
                Write(current, key, child);
                current = child!;
            }
        }

        private static object NewContainer(PathSegment next)
            => next is IndexSegment ? new List<object?>() : new Dictionary<string, object?>();

        private static object? Read(object container, object? key) => container switch
        {
            IList list => PathNode.ListIndex(key, list.Count) is var i && i >= 0 && i < list.Count ? list[i] : null,
            _ => PathNode.Member(container, Values.Format(key))
        };

        private static void Write(object container, object? key, object? value)
        {
            switch (container)
            {
                case IDictionary<string, object?> dict:
                    dict[Values.Format(key)] = value;
                    break;
                case IList list:
                    int i = PathNode.ListIndex(key, list.Count);
                    if (i < 0)
                        throw new InvalidOperationException($"Index {Values.Format(key)} is before the start of the list.");
                    while (list.Count <= i)
                        list.Add(null);
                    list[i] = value;
                    break;
                default:
                    throw new InvalidOperationException($"Cannot set '{Values.Format(key)}' on {Values.Describe(container)}.");
            }
        }
    }
}
