// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;

namespace EchoPhase.Clients.Helpers
{
    public class ModelDictionaryBuilder : DataBuilderBase<ModelDictionaryBuilder>
    {
        public override Dictionary<string, object?> Build(object? obj)
        {
            if (obj == null)
                return new Dictionary<string, object?>();

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return BuildInternal(obj, visited);
        }

        private Dictionary<string, object?> BuildInternal(object? obj, HashSet<object> visited)
        {
            if (obj == null)
                return new Dictionary<string, object?>();

            var result = new Dictionary<string, object?>();
            Type type = obj.GetType();

            if (IsSimple(type))
                return result;

            if (_options.IncludeProperties)
            {
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.CanRead);

                foreach (var prop in props)
                {
                    object? value = prop.GetValue(obj);
                    result[prop.Name] = TransformValue(value, visited);
                }
            }

            if (_options.IncludeFields)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    object? value = field.GetValue(obj);
                    result[field.Name] = TransformValue(value, visited);
                }
            }

            return result;
        }

        private object? TransformValue(object? value, HashSet<object> visited)
        {
            if (value == null) return null;

            Type type = value.GetType();

            if (IsSimple(type)) return value;

            if (!visited.Add(value))
                return null;

            try
            {
                if (IsEnumerable(type))
                {
                    var list = new List<object?>();
                    foreach (var item in (System.Collections.IEnumerable)value)
                    {
                        if (item == null)
                        {
                            list.Add(null);
                        }
                        else if (IsSimple(item.GetType()))
                        {
                            list.Add(item);
                        }
                        else
                        {
                            list.Add(BuildInternal(item, visited));
                        }
                    }
                    return list;
                }

                return BuildInternal(value, visited);
            }
            finally
            {
                visited.Remove(value);
            }
        }
    }
}
