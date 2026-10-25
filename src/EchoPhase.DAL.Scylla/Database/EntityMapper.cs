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
                        : row.GetValue(prop.PropertyType, columnName);
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

        public static TEntity Map<TEntity>(Row row, IEntityBuilder builder) =>
            (TEntity)Map(row, typeof(TEntity), builder);
    }
}
