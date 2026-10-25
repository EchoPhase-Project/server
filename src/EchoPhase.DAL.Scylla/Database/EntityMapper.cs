// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using Cassandra;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Database
{
    internal static class EntityMapper
    {
        public static object Map(Row row, Type entityType, IEntityBuilder builder)
        {
            var entity = Activator.CreateInstance(entityType)
                ?? throw new InvalidOperationException($"Cannot create an instance of {entityType.Name}");

            foreach (var propertyName in builder.GetMappedProperties())
            {
                var prop = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                    continue;

                var columnName = builder.GetColumn(prop.Name);

                // Absent from a projection: nothing to map.
                if (row.GetColumn(columnName) == null)
                    continue;

                object? value;
                try
                {
                    value = builder.GetConverter(prop.Name) is { } converter
                        ? converter.FromProvider(row.GetValue(converter.ProviderType, columnName))
                        : Coerce(row.GetValue(prop.PropertyType, columnName), prop.PropertyType);
                }
                catch (Exception ex)
                {
                    // Silently skipping would return entities with default values
                    // that look valid; a schema/type mismatch must be visible.
                    throw new InvalidOperationException(
                        $"Cannot map column '{columnName}' to {entityType.Name}.{prop.Name} ({prop.PropertyType.Name}).", ex);
                }

                if (value != null && value != DBNull.Value)
                    prop.SetValue(entity, value);
            }

            return entity;
        }

        // The driver hands collections back in its own types (a SortedDictionary
        // for a map, an array for a list): make them the property's.
        private static object? Coerce(object? value, Type target)
        {
            if (value is null || target.IsInstanceOfType(value) || value is string)
                return value;

            if (value is System.Collections.IDictionary map && target.IsGenericType)
            {
                var concrete = target.IsInterface
                    ? typeof(Dictionary<,>).MakeGenericType(target.GetGenericArguments())
                    : target;
                var result = (System.Collections.IDictionary)Activator.CreateInstance(concrete)!;
                foreach (var key in map.Keys)
                    result[key] = map[key];
                return result;
            }

            if (value is System.Collections.IEnumerable items)
            {
                var element = target.IsArray ? target.GetElementType()! : target.IsGenericType ? target.GetGenericArguments()[0] : typeof(object);
                var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
                foreach (var item in items)
                    list.Add(item);
                if (target.IsArray)
                {
                    var array = Array.CreateInstance(element, list.Count);
                    list.CopyTo(array, 0);
                    return array;
                }
                if (target.IsInstanceOfType(list))
                    return list;
                return Activator.CreateInstance(target.IsInterface ? typeof(HashSet<>).MakeGenericType(element) : target, list);
            }

            return value;
        }

        public static TEntity Map<TEntity>(Row row, IEntityBuilder builder) =>
            (TEntity)Map(row, typeof(TEntity), builder);
    }
}
