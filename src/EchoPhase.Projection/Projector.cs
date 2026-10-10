// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using EchoPhase.Projection.Attributes;
using EchoPhase.Projection.Options;

namespace EchoPhase.Projection
{
    public class Projector
    {
        internal readonly ProjectionOptions _defaultOptions;

        private static readonly ConcurrentDictionary<Type, (PropertyInfo Prop, bool IsExposed)[]> PropCache = new();

        public Projector() => _defaultOptions = new ProjectionOptions();
        public Projector(ProjectionOptions defaultOptions) => _defaultOptions = defaultOptions;

        public ProjectionBuilder<T> For<T>(T source) =>
            new(source, _defaultOptions.Clone());

        public CollectionProjectionBuilder<T> ForCollection<T>(IEnumerable<T> source) =>
            new(source, _defaultOptions.Clone());

        // ── Entry point ───────────────────────────────────────────────────────

        /// <param name="exclusive">
        /// Only the listed fields (<see cref="ProjectionBuilder{T}.Only"/>): exposed
        /// properties are not added next to them.
        /// </param>
        internal object? Execute<T>(
            T source,
            ProjectionOptions options,
            IReadOnlyList<Expression<Func<T, object?>>> includeProperties,
            IReadOnlyDictionary<string, ICollectionConfig>? collectionConfigs = null,
            bool exclusive = false)
        {
            if (source == null) return null;

            ISet<string>? paths = includeProperties.Count > 0
                ? includeProperties.Select(ExtractMemberPath).ToHashSet(StringComparer.Ordinal)
                : null;

            var context = new Context(
                options,
                MergeExposed: options.IncludeOnlyExpose && !exclusive,
                collectionConfigs ?? new Dictionary<string, ICollectionConfig>());

            context.Visited.Add(source);
            var result = context.NewObject();
            FillObject(result, source, paths, context);
            return result;
        }

        /// <summary>
        /// One projection run. Selection at each level: with no paths (nothing
        /// included at or above it), every property, or only [Expose] ones under
        /// IncludeOnlyExpose; with paths, the listed ones and the ancestors of
        /// deeper ones, plus the exposed ones unless the run is exclusive.
        /// </summary>
        private sealed record Context(
            ProjectionOptions Options,
            bool MergeExposed,
            IReadOnlyDictionary<string, ICollectionConfig> CollectionConfigs)
        {
            public HashSet<object> Visited { get; } = new(ReferenceEqualityComparer.Instance);

            public IDictionary<string, object?> NewObject() =>
                Options.UseExpando ? new ExpandoObject() : new Dictionary<string, object?>();
        }

        // ── Projection ────────────────────────────────────────────────────────

        private object? ConvertToObject(
            object? source, ISet<string>? paths, Context context,
            ICollectionConfig? collectionConfig = null)
        {
            if (source == null) return null;

            if (source is JsonElement json)
            {
                switch (json.ValueKind)
                {
                    case JsonValueKind.Object:
                        var obj = context.NewObject();
                        FillObject(obj, json, paths, context);
                        return obj;
                    case JsonValueKind.Array:
                        return json.EnumerateArray()
                            .Select(item => ConvertToObject(item, paths, context))
                            .ToList();
                    default:
                        return GetJsonSimpleValue(json);
                }
            }

            if (IsSimple(source.GetType())) return source;

            if (source is IDictionary or IEnumerable)
            {
                // Cycles through collections too (a list containing its owner).
                if (!context.Visited.Add(source)) return null;
                try
                {
                    if (source is IDictionary dictionary)
                    {
                        var obj = context.NewObject();
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            var key = entry.Key.ToString() ?? "";
                            // Dictionary keys carry no [Expose]: they count as exposed.
                            if (!Includes(paths, key, isExposed: true, context)) continue;
                            obj[key] = ConvertToObject(entry.Value, ChildPaths(paths, key, context), context);
                        }
                        return obj;
                    }

                    var list = new List<object?>();
                    foreach (var item in (IEnumerable)source)
                    {
                        list.Add(item is not null && collectionConfig is not null
                            ? collectionConfig.ApplyBoxed(this, item).Build()
                            : ConvertToObject(item, paths, context));
                    }
                    return list;
                }
                finally
                {
                    context.Visited.Remove(source);
                }
            }

            if (!context.Visited.Add(source)) return null;
            try
            {
                var obj = context.NewObject();
                FillObject(obj, source, paths, context);
                return obj;
            }
            finally
            {
                context.Visited.Remove(source);
            }
        }

        private void FillObject(IDictionary<string, object?> target, object source, ISet<string>? paths, Context context)
        {
            if (source is JsonElement json)
            {
                foreach (var prop in json.EnumerateObject())
                {
                    if (!Includes(paths, prop.Name, isExposed: true, context)) continue;
                    target[prop.Name] = ConvertToObject(prop.Value, ChildPaths(paths, prop.Name, context), context);
                }
                return;
            }

            foreach (var (prop, isExposed) in GetCachedProperties(source.GetType()))
            {
                if (!Includes(paths, prop.Name, isExposed, context)) continue;

                context.CollectionConfigs.TryGetValue(prop.Name, out var collectionConfig);
                target[prop.Name] = ConvertToObject(
                    prop.GetValue(source), ChildPaths(paths, prop.Name, context), context, collectionConfig);
            }
        }

        private static bool Includes(ISet<string>? paths, string name, bool isExposed, Context context)
        {
            if (paths == null) return isExposed || !context.Options.IncludeOnlyExpose;
            if (context.MergeExposed && isExposed) return true;
            if (paths.Contains(name)) return true;

            var prefix = name + ".";
            foreach (var path in paths)
                if (path.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// The paths below <paramref name="name"/>. Null (its whole content, by
        /// the [Expose] rule) when nothing deeper is listed or when the parent
        /// level selected everything, and, with IncludeSubPaths or
        /// IncludeNestedContent, when the property itself is listed.
        /// </summary>
        private static ISet<string>? ChildPaths(ISet<string>? paths, string name, Context context)
        {
            if (paths == null) return null;

            var listed = paths.Contains(name);
            if (listed && context.Options.IncludeNestedContent) return null;

            var prefix = name + ".";
            HashSet<string>? deeper = null;
            foreach (var path in paths)
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                deeper ??= new HashSet<string>(StringComparer.Ordinal);
                deeper.Add(path[prefix.Length..]);
            }

            if (deeper != null) return deeper;
            return listed && !context.Options.IncludeSubPaths
                ? new HashSet<string>(StringComparer.Ordinal)
                : null;
        }

        // ── Path / reflection helpers ─────────────────────────────────────────

        private static (PropertyInfo Prop, bool IsExposed)[] GetCachedProperties(Type type) =>
            PropCache.GetOrAdd(type, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.GetIndexParameters().Length == 0)
                 .Select(p => (p, Attribute.IsDefined(p, typeof(ExposeAttribute))))
                 .ToArray());

        internal static string ExtractMemberPath<T>(Expression<Func<T, object?>> expr)
        {
            Expression body = expr.Body;
            while (body.NodeType == ExpressionType.Convert || body.NodeType == ExpressionType.ConvertChecked)
                body = ((UnaryExpression)body).Operand;

            if (body is MemberExpression member)
            {
                var sb = new StringBuilder();
                BuildPath(member, sb);
                return sb.ToString();
            }

            throw new InvalidOperationException("Unsupported expression: " + expr);
        }

        private static void BuildPath(MemberExpression member, StringBuilder sb)
        {
            if (member.Expression is MemberExpression parent)
            {
                BuildPath(parent, sb);
                sb.Append('.');
            }
            sb.Append(member.Member.Name);
        }

        /// <summary>Values serialized as they are, not projected property by property.</summary>
        private static bool IsSimple(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum ||
                type == typeof(string) || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan) ||
                type == typeof(Guid) || type == typeof(Uri) || type == typeof(Version) ||
                type == typeof(Half) || type == typeof(Int128) || type == typeof(UInt128) ||
                type == typeof(System.Numerics.BigInteger);
        }

        private static object? GetJsonSimpleValue(JsonElement elem) => elem.ValueKind switch
        {
            JsonValueKind.String => elem.GetString(),
            JsonValueKind.Number => elem.TryGetInt64(out long l) ? l
                                  : elem.TryGetDouble(out double d) ? d
                                  : (object?)elem.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => elem.GetRawText()
        };
    }
}
