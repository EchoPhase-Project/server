// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Parse once, evaluate many times: programs evaluate the same
    /// conditions and texts on every loop iteration and every run. Each cache
    /// keeps the <see cref="Capacity"/> most recently used sources.
    /// </summary>
    internal static class Compiled
    {
        public const int Capacity = 4096;

        private static readonly ParseCache<Node> Expressions = new(Capacity, source => TieredNode.Wrap(ExpressionParser.Parse(source)));
        private static readonly ParseCache<Template> Templates = new(Capacity, Engine.Template.Parse);
        private static readonly ParseCache<PathNode> Paths = new(Capacity, ExpressionParser.ParsePath);

        public static Node Expression(string source) => Expressions.Get(source);

        public static Template Template(string source) => Templates.Get(source);

        public static PathNode Path(string source) => Paths.Get(source);

        /// <summary>A path to assign to: no slices.</summary>
        public static PathNode Target(string source)
        {
            var path = Path(source);
            return path.Segments.Any(s => s is SliceSegment)
                ? throw new ScriptSyntaxException("Cannot assign to a slice", 0)
                : path;
        }


        /// <summary>
        /// Assigns <paramref name="value"/> at a path, creating the
        /// dictionaries and lists on the way: <c>a.b[2].c</c> on empty
        /// variables makes a = { b: [null, null, { c: value }] }.
        /// </summary>
        public static void Assign(PathNode path, IDictionary<string, object> variables, object? value)
        {
            if (path.Segments.Length == 0)
            {
                variables[path.Root] = value!;
                return;
            }

            if (!variables.TryGetValue(path.Root, out var current) || current is null)
                variables[path.Root] = current = NewContainer(path.Segments[0]);
            else if (Frozen.Thaw(current) is { } own && !ReferenceEquals(own, current))
                variables[path.Root] = current = own;

            for (int i = 0; i < path.Segments.Length; i++)
            {
                var segment = path.Segments[i];
                bool last = i == path.Segments.Length - 1;
                object? key = segment is IndexSegment ix ? Values.Unwrap(ix.Index.Evaluate(variables)) : ((MemberSegment)segment).Name;

                // A shared constant on the way is copied, never written into.
                var child = last ? value : Frozen.Thaw(Read(current, key)) ?? NewContainer(path.Segments[i + 1]);
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
