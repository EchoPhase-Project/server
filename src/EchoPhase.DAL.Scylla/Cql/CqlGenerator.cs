// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Cql
{
    public class CqlGenerator : ICqlGenerator
    {
        public (string cql, object[] parameters) GenerateInsert(object entity, IEntityBuilder builder)
        {
            var type = entity.GetType();
            var props = GetWritableProperties(type);

            var columns = props.Select(p => builder.GetColumn(p.Name)).ToList();
            var placeholders = Enumerable.Repeat("?", columns.Count);
            var values = props.Select(p => p.GetValue(entity) ?? DBNull.Value).ToArray();

            var cql = $"INSERT INTO {builder.GetTableName()} " +
                      $"({string.Join(", ", columns)}) " +
                      $"VALUES ({string.Join(", ", placeholders)})";

            return (cql, values);
        }

        public (string cql, object[] parameters) GenerateUpdate(object entity, IEntityBuilder builder)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());
            var type = entity.GetType();

            var props = GetWritableProperties(type)
                .Where(p => !pkNames.Contains(p.Name))
                .ToList();

            var setClauses = props.Select(p => $"{builder.GetColumn(p.Name)} = ?");
            var values = new List<object>(props.Select(p => p.GetValue(entity) ?? DBNull.Value));

            values.AddRange(GetPrimaryKeyValues(entity, pkNames));

            var whereClause = BuildWhereClause(builder, pkNames);
            var cql = $"UPDATE {builder.GetTableName()} " +
                      $"SET {string.Join(", ", setClauses)} " +
                      $"WHERE {whereClause}";

            return (cql, values.ToArray());
        }

        public (string cql, object[] parameters) GenerateDelete(object entity, IEntityBuilder builder)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());
            var values = GetPrimaryKeyValues(entity, pkNames);
            var whereClause = BuildWhereClause(builder, pkNames);

            var cql = $"DELETE FROM {builder.GetTableName()} WHERE {whereClause}";
            return (cql, values.ToArray());
        }

        private static List<PropertyInfo> GetWritableProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToList();

        private static IReadOnlyList<string> ValidatePrimaryKey(IEntityBuilder builder, Type entityType)
        {
            var pkNames = builder.GetPrimaryKey();
            if (pkNames == null || !pkNames.Any())
                throw new InvalidOperationException(
                    $"Primary key not defined for {entityType.Name}");
            return pkNames;
        }

        private static object[] GetPrimaryKeyValues(object entity, IReadOnlyList<string> pkNames)
        {
            var type = entity.GetType();
            return pkNames.Select(pk =>
            {
                var prop = type.GetProperty(pk, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"Primary key property {pk} not found");
                return prop.GetValue(entity) ?? DBNull.Value;
            }).ToArray();
        }

        private static string BuildWhereClause(IEntityBuilder builder, IReadOnlyList<string> pkNames) =>
            string.Join(" AND ", pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));
    }
}
