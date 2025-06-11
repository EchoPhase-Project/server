using System.Collections;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using EchoPhase.Attributes;
using EchoPhase.Helpers.Options;
using Newtonsoft.Json.Linq;

namespace EchoPhase.Helpers
{
    public class ProjectionHelper
    {
        private ProjectionOptions _options = new();

        public ProjectionHelper()
        {
        }

        public ProjectionHelper WithOptions(ProjectionOptions options)
        {
            _options = options;
            return this;
        }

        public ProjectionHelper WithOptions(Action<ProjectionOptions> configure)
        {
            configure(_options);
            return this;
        }

        public object Project<T>(
            T source,
            params Expression<Func<T, object?>>[] includeProperties)
        {
            if (source == null) return null!;

            var fields = includeProperties.Select(ExtractMemberPath).ToHashSet();

            if (fields is { Count: 0 })
                fields = _options.IncludeOnlyExpose ? new HashSet<string>() : null;

            if (fields != null)
                CollectExposePaths(fields, typeof(T), "");

            return _options.UseExpando
                ? (object)ProjectToExpando(source!, fields, 0)
                : (object)ProjectToDictionary(source!, fields, 0);
        }

        private Dictionary<string, object?> ProjectToDictionary(
            object source,
            ISet<string>? includePaths,
            int depth
        )
        {
            var dict = new Dictionary<string, object?>();
            FillDict(dict, source, includePaths, depth, ProjectToDictionary);

            return dict;
        }

        private ExpandoObject ProjectToExpando(
            object source,
            ISet<string>? includePaths,
            int depth
        )
        {
            var expando = new ExpandoObject();
            FillDict((IDictionary<string, object?>)expando, source, includePaths, depth, ProjectToExpando);

            return expando;
        }

        private void CollectExposePaths(in ISet<string> fields, Type type, string basePath)
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var path = string.IsNullOrEmpty(basePath) ? prop.Name : $"{basePath}.{prop.Name}";

                if (Attribute.IsDefined(prop, typeof(ExposeAttribute)))
                    fields.Add(path);

                if (!IsSimple(prop.PropertyType))
                    CollectExposePaths(fields, prop.PropertyType, path);
            }
        }

        private object? ConvertToObject(
            object source,
            ISet<string>? includePaths,
            int depth,
            Func<object, ISet<string>?, int, object?> recursion)
        {
            if (source == null)
                return null;

            if (source is JsonElement jsonElem)
            {
                switch (jsonElem.ValueKind)
                {
                    case JsonValueKind.Object:
                        var dict = new Dictionary<string, object?>();
                        FillDict(dict, jsonElem, includePaths, depth, recursion);
                        return dict;

                    case JsonValueKind.Array:
                        var list = new List<object?>();
                        int idx = 0;
                        foreach (var item in jsonElem.EnumerateArray())
                        {
                            // Для массива нет ключа, передаём null в includePaths или строим пути по индексу
                            list.Add(ConvertToObject(item, null, depth + 1, recursion));
                            idx++;
                        }
                        return list;

                    default:
                        // Простые типы
                        return GetJsonSimpleValue(jsonElem);
                }
            }

            if (source is JToken jtoken)
            {
                switch (jtoken.Type)
                {
                    case JTokenType.Object:
                        var dict = new Dictionary<string, object?>();
                        FillDict(dict, jtoken, includePaths, depth, recursion);
                        return dict;

                    case JTokenType.Array:
                        var list = new List<object?>();
                        foreach (var item in (JArray)jtoken)
                        {
                            list.Add(ConvertToObject(item, null, depth + 1, recursion));
                        }
                        return list;

                    case JTokenType.Null:
                        return null;

                    default:
                        if (jtoken is JValue jval)
                            return jval.Value;
                        else
                            return jtoken.ToString();
                }
            }

            if (source is IDictionary rawDict && !(source is string))
            {
                var dict = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in rawDict)
                {
                    string key = entry.Key?.ToString() ?? "";
                    if (includePaths != null && !includePaths.Contains(key))
                        continue;

                    dict[key] = ConvertToObject(entry.Value!, null, depth + 1, recursion);
                }
                return dict;
            }

            if (source is IEnumerable enumerable && !(source is string))
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(ConvertToObject(item!, null, depth + 1, recursion));
                }
                return list;
            }

            if (!IsSimple(source.GetType()))
            {
                var dict = new Dictionary<string, object?>();
                FillDict(dict, source, includePaths, depth, recursion);
                return dict;
            }

            return source;
        }

        private void FillDict(
            IDictionary<string, object?> dict,
            object source,
            ISet<string>? includePaths,
            int depth,
            Func<object, ISet<string>?, int, object?> recursion)
        {
            bool ShouldInclude(string path) =>
                includePaths == null ||
                includePaths.Contains(path) ||
                (_options.IncludeSubPaths && includePaths.Any(p => p.StartsWith(path + ".")));

            if (source is JsonElement jsonElem)
            {
                foreach (var prop in jsonElem.EnumerateObject())
                {
                    if (!ShouldInclude(prop.Name))
                        continue;

                    dict[prop.Name] = ConvertToObject(prop.Value, SubPaths(includePaths, prop.Name), depth + 1, recursion);
                }
                return;
            }

            if (source is JObject jobject)
            {
                foreach (var prop in jobject.Properties())
                {
                    if (!ShouldInclude(prop.Name))
                        continue;

                    dict[prop.Name] = ConvertToObject(prop.Value, SubPaths(includePaths, prop.Name), depth + 1, recursion);
                }
                return;
            }

            var type = source.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                           .Where(p => p.GetIndexParameters().Length == 0);
            foreach (var prop in props)
            {
                if (!ShouldInclude(prop.Name))
                    continue;

                var val = prop.GetValue(source);
                dict[prop.Name] = ConvertToObject(val!, SubPaths(includePaths, prop.Name), depth + 1, recursion);
            }
        }

        private string ExtractMemberPath<T>(Expression<Func<T, object?>> expr)
        {
            Expression body = expr.Body;

            while (body.NodeType == ExpressionType.Convert || body.NodeType == ExpressionType.ConvertChecked)
            {
                body = ((UnaryExpression)body).Operand;
            }

            if (body is MemberExpression member)
            {
                var names = new Stack<string>();
                while (member != null)
                {
                    names.Push(member.Member.Name);
                    if (member.Expression is MemberExpression parent)
                        member = parent;
                    else
                        break;
                }
                var path = string.Join(".", names);
                return path;
            }

            throw new InvalidOperationException("Unsupported expression: " + expr);
        }

        private ISet<string>? SubPaths(ISet<string>? paths, string parent)
        {
            var prefix = parent + ".";
            var sub = paths?
                .Where(p => p.StartsWith(prefix))
                .Select(p => p[prefix.Length..])
                .ToHashSet();

            return (sub == null || sub.Count == 0) ? null : sub;
        }

        protected static bool IsSimple(Type type)
        {
            return
                type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(Guid) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan);
        }

        static object? GetJsonSimpleValue(JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String: return elem.GetString();
                case JsonValueKind.Number:
                    if (elem.TryGetInt64(out long l)) return l;
                    if (elem.TryGetDouble(out double d)) return d;
                    return elem.GetRawText();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null: return null;
                default: return elem.GetRawText();
            }
        }
    }
}
