// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using Cassandra;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Cql
{
    /// <remarks>
    /// One rule for null on every write: a null property means "leave the
    /// stored value unchanged" and is sent as <see cref="Unset"/>, so no
    /// tombstone is written. Clearing a value is explicit
    /// (<see cref="GenerateClearColumns"/>).
    /// </remarks>
    public class CqlGenerator : ICqlGenerator
    {
        public IReadOnlyList<(string cql, object[] parameters)> Generate(TrackedEntity tracked, IEntityBuilder builder)
        {
            var statements = new List<(string, object[])>();

            switch (tracked.State)
            {
                case EntityState.Added:
                    statements.Add(GenerateInsert(tracked.Entity, builder, tracked.Options));
                    break;
                case EntityState.Modified:
                    // Only clearing requested: the column delete alone is the update.
                    if (tracked.ClearedColumns.Count == 0 || HasValuesToSet(tracked, builder))
                        statements.Add(GenerateUpdate(tracked.Entity, builder, tracked.Options, tracked.ClearedColumns));
                    break;
                case EntityState.Deleted:
                    // The row is gone; clearing its columns too would be redundant.
                    statements.Add(GenerateDelete(tracked.Entity, builder, tracked.Options));
                    return statements;
                default:
                    return statements;
            }

            if (tracked.ClearedColumns.Count > 0)
                statements.Add(GenerateClearColumns(tracked.Entity, builder, tracked.ClearedColumns, tracked.Options));

            return statements;
        }

        public (string cql, object[] parameters) GenerateInsert(object entity, IEntityBuilder builder, WriteOptions? options = null)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());
            GetPrimaryKeyValues(entity, pkNames);

            var props = GetMappedProperties(entity.GetType(), builder);
            var columns = props.Select(p => builder.GetColumn(p.Name)).ToList();
            var placeholders = Enumerable.Repeat("?", columns.Count);

            var values = new List<object>(props.Select(p => ValueOrUnset(p, entity)));
            var (usingClause, usingValues) = BuildUsing(options, allowTtl: true);
            values.AddRange(usingValues);

            var cql = $"INSERT INTO {builder.GetFullTableName()} " +
                      $"({string.Join(", ", columns)}) " +
                      $"VALUES ({string.Join(", ", placeholders)}){usingClause}";

            return (cql, values.ToArray());
        }

        public (string cql, object[] parameters) GenerateUpdate(object entity, IEntityBuilder builder, WriteOptions? options = null)
            => GenerateUpdate(entity, builder, options, excluded: null);

        private (string cql, object[] parameters) GenerateUpdate(
            object entity, IEntityBuilder builder, WriteOptions? options, IReadOnlySet<string>? excluded)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());

            var props = GetMappedProperties(entity.GetType(), builder)
                .Where(p => !pkNames.Contains(p.Name) && excluded?.Contains(p.Name) != true)
                .ToList();

            // UPDATE needs at least one SET clause; for key-only entities an INSERT is the equivalent upsert.
            if (props.Count == 0)
                return GenerateInsert(entity, builder, options);

            var (usingClause, usingValues) = BuildUsing(options, allowTtl: true);

            // Bind markers are positional: USING comes before SET in the text.
            var values = new List<object>(usingValues);
            values.AddRange(props.Select(p => ValueOrUnset(p, entity)));
            values.AddRange(GetPrimaryKeyValues(entity, pkNames));

            var setClauses = props.Select(p => $"{builder.GetColumn(p.Name)} = ?");
            var cql = $"UPDATE {builder.GetFullTableName()}{usingClause} " +
                      $"SET {string.Join(", ", setClauses)} " +
                      $"WHERE {BuildWhereClause(builder, pkNames)}";

            return (cql, values.ToArray());
        }

        public (string cql, object[] parameters) GenerateDelete(object entity, IEntityBuilder builder, WriteOptions? options = null)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());
            var (usingClause, usingValues) = BuildUsing(options, allowTtl: false);

            var values = new List<object>(usingValues);
            values.AddRange(GetPrimaryKeyValues(entity, pkNames));

            var cql = $"DELETE FROM {builder.GetFullTableName()}{usingClause} WHERE {BuildWhereClause(builder, pkNames)}";
            return (cql, values.ToArray());
        }

        public (string cql, object[] parameters) GenerateClearColumns(
            object entity, IEntityBuilder builder, IReadOnlyCollection<string> properties, WriteOptions? options = null)
        {
            if (properties.Count == 0)
                throw new ArgumentException("At least one column must be cleared.", nameof(properties));

            var pkNames = ValidatePrimaryKey(builder, entity.GetType());

            var keyColumns = properties.Where(pkNames.Contains).ToList();
            if (keyColumns.Count > 0)
                throw new InvalidOperationException(
                    $"Primary key columns cannot be cleared: {string.Join(", ", keyColumns)}. Delete the row instead.");

            var (usingClause, usingValues) = BuildUsing(options, allowTtl: false);

            var values = new List<object>(usingValues);
            values.AddRange(GetPrimaryKeyValues(entity, pkNames));

            var columns = properties.Select(builder.GetColumn);
            var cql = $"DELETE {string.Join(", ", columns)} FROM {builder.GetFullTableName()}{usingClause} " +
                      $"WHERE {BuildWhereClause(builder, pkNames)}";

            return (cql, values.ToArray());
        }

        private static (string clause, object[] values) BuildUsing(WriteOptions? options, bool allowTtl)
        {
            if (options is null || options.IsEmpty)
                return (string.Empty, []);

            options.Validate();

            if (options.Ttl is not null && !allowTtl)
                throw new InvalidOperationException("USING TTL applies to inserts and updates only, not to deletes.");

            var parts = new List<string>();
            var values = new List<object>();

            if (options.TtlSeconds is { } ttl)
            {
                parts.Add("TTL ?");
                values.Add(ttl);
            }

            if (options.TimestampMicros is { } timestamp)
            {
                parts.Add("TIMESTAMP ?");
                values.Add(timestamp);
            }

            return ($" USING {string.Join(" AND ", parts)}", values.ToArray());
        }

        private static bool HasValuesToSet(TrackedEntity tracked, IEntityBuilder builder)
        {
            var pkNames = builder.GetPrimaryKey();
            return GetMappedProperties(tracked.EntityType, builder)
                .Any(p => !pkNames.Contains(p.Name)
                          && !tracked.ClearedColumns.Contains(p.Name)
                          && p.GetValue(tracked.Entity) is not null);
        }

        // Unset leaves null columns untouched instead of writing tombstones.
        private static object ValueOrUnset(PropertyInfo property, object entity)
            => property.GetValue(entity) ?? Unset.Value;

        private static List<PropertyInfo> GetMappedProperties(Type type, IEntityBuilder builder) =>
            builder.GetMappedProperties()
                .Concat(builder.GetPrimaryKey())
                .Distinct()
                .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
                .Where(p => p is { CanRead: true })
                .ToList()!;

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

                return prop.GetValue(entity)
                    ?? throw new InvalidOperationException($"Primary key property {pk} of {type.Name} is null");
            }).ToArray();
        }

        private static string BuildWhereClause(IEntityBuilder builder, IReadOnlyList<string> pkNames) =>
            string.Join(" AND ", pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));
    }
}
