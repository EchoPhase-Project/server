using System.Reflection;

namespace EchoPhase.Helpers.Builders
{
    public class ModelDictionaryBuilder : DataBuilderBase<ModelDictionaryBuilder>
    {
        public override Dictionary<string, object?> Build(object? obj)
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
                    result[prop.Name] = TransformValue(value);
                }
            }

            if (_options.IncludeFields)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    object? value = field.GetValue(obj);
                    result[field.Name] = TransformValue(value);
                }
            }

            return result;
        }

        private object? TransformValue(object? value)
        {
            if (value == null) return null;

            Type type = value.GetType();

            if (IsSimple(type)) return value;

            if (IsEnumerable(type))
            {
                var list = new List<object?>();
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    list.Add(IsSimple(item?.GetType() ?? typeof(object)) ? item : Build(item));
                }
                return list;
            }

            return Build(value);
        }
    }
}
