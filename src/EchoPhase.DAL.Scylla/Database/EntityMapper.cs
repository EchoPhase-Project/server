// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Cassandra;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Database
{
    internal static class EntityMapper
    {
        private sealed record Column(string Name, string Property, Type Type, ValueConverter? Converter, Action<object, object?> Set);

        private sealed record Plan(Func<object> Create, Column[] Columns);

        // Per entity type and model: what to read and how to set it, with compiled
        // setters instead of reflection on every row.
        private static readonly ConcurrentDictionary<(Type, IEntityBuilder), Plan> Plans = new();

        public static object Map(Row row, Type entityType, IEntityBuilder builder)
        {
            var plan = Plans.GetOrAdd((entityType, builder), key => BuildPlan(key.Item1, key.Item2));
            var entity = plan.Create();

            foreach (var column in plan.Columns)
            {
                // Absent from a projection: nothing to map.
                if (row.GetColumn(column.Name) == null)
                    continue;

                object? value;
                try
                {
                    value = column.Converter is { } converter
                        ? converter.FromProvider(row.GetValue(converter.ProviderType, column.Name))
                        : Coerce(row.GetValue(column.Type, column.Name), column.Type);
                }
                catch (Exception ex)
                {
                    // Silently skipping would return entities with default values
                    // that look valid; a schema/type mismatch must be visible.
                    throw new InvalidOperationException(
                        $"Cannot map column '{column.Name}' to {entityType.Name}.{column.Property} ({column.Type.Name}).", ex);
                }

                if (value != null && value != DBNull.Value)
                    column.Set(entity, value);
            }

            return entity;
        }

        private static Plan BuildPlan(Type entityType, IEntityBuilder builder)
        {
            if (entityType.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException($"Cannot create an instance of {entityType.Name}: it needs a parameterless constructor.");
            var create = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(entityType), typeof(object))).Compile();

            var columns = new List<Column>();
            foreach (var propertyName in builder.GetMappedProperties())
            {
                var prop = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop is not { CanWrite: true })
                    continue;

                var target = Expression.Parameter(typeof(object), "entity");
                var value = Expression.Parameter(typeof(object), "value");
                var set = Expression.Lambda<Action<object, object?>>(
                    Expression.Assign(
                        Expression.Property(Expression.Convert(target, entityType), prop),
                        Expression.Convert(value, prop.PropertyType)),
                    target, value).Compile();

                columns.Add(new Column(builder.GetColumn(prop.Name), prop.Name, prop.PropertyType, builder.GetConverter(prop.Name), set));
            }
            return new Plan(create, columns.ToArray());
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
