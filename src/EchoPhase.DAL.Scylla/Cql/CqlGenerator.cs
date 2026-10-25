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
        public IReadOnlyList<(string cql, object?[] parameters)> Generate(TrackedEntity tracked, IEntityBuilder builder)
        {
            var statements = new List<(string, object?[])>();
            var condition = ConditionFor(tracked, builder);
            var overrides = builder.ConcurrencyToken is { } token && tracked.NewToken is not null
                ? new Dictionary<string, object?> { [token] = tracked.NewToken }
                : null;

            switch (tracked.State)
            {
                case EntityState.Added:
                    statements.Add(GenerateInsert(tracked.Entity, builder, tracked.Options, overrides, condition));
                    break;
                case EntityState.Modified when tracked.Changed is { Count: > 0 } changed:
                    // Found changed: only those properties; null among them clears.
                    var unchanged = builder.GetMappedProperties()
                        .Where(p => !changed.Contains(p) && p != builder.ConcurrencyToken).ToHashSet();
                    var nulls = changed.Where(p => tracked.EntityType.GetProperty(p)?.GetValue(tracked.Entity) is null)
                        .Concat(tracked.ClearedColumns).ToHashSet();
                    statements.Add(GenerateUpdate(tracked.Entity, builder, tracked.Options, excluded: unchanged,
                        cleared: nulls, overrides, condition));
                    return statements;
                case EntityState.Modified when condition is not null:
                    // One conditional statement: cleared columns are set to null in it.
                    statements.Add(GenerateUpdate(tracked.Entity, builder, tracked.Options, excluded: null,
                        cleared: tracked.ClearedColumns, overrides, condition));
                    return statements;
                case EntityState.Modified:
                    // Only clearing requested: the column delete alone is the update.
                    if (tracked.ClearedColumns.Count == 0 || HasValuesToSet(tracked, builder))
                        statements.Add(GenerateUpdate(tracked.Entity, builder, tracked.Options, tracked.ClearedColumns,
                            cleared: null, overrides: null, condition: null));
                    break;
                case EntityState.Deleted:
                    // The row is gone; clearing its columns too would be redundant.
                    statements.Add(GenerateDelete(tracked.Entity, builder, tracked.Options, condition));
                    return statements;
                default:
                    return statements;
            }

            if (tracked.ClearedColumns.Count > 0)
                statements.Add(GenerateClearColumns(tracked.Entity, builder, tracked.ClearedColumns, tracked.Options));

            return statements;
        }

        /// <summary>
        /// The IF clause of a tracked write: its own condition, and with a
        /// concurrency token, IF NOT EXISTS for an add or IF token = (the value
        /// read) for an update or delete.
        /// </summary>
        public static WriteCondition? ConditionFor(TrackedEntity tracked, IEntityBuilder builder)
        {
            if (builder.ConcurrencyToken is not { } token)
                return tracked.Condition;

            if (tracked.State == EntityState.Added)
                return tracked.Condition ?? WriteCondition.NotExists;

            var checks = (tracked.Condition?.Checks ?? []).ToList();
            checks.Add(new ConditionCheck(token, "=", Reflect.Property(tracked.EntityType, token).GetValue(tracked.Entity)));
            return new WriteCondition(false, false, checks);
        }

        public (string cql, object?[] parameters) GenerateInsert(object entity, IEntityBuilder builder, WriteOptions? options = null)
            => GenerateInsert(entity, builder, options, overrides: null, condition: null);

        public (string cql, object?[] parameters) GenerateInsert(object entity, IEntityBuilder builder, WriteOptions? options,
            IReadOnlyDictionary<string, object?>? overrides, WriteCondition? condition)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());
            GetPrimaryKeyValues(entity, pkNames, builder);
            if (condition is { IfExists: true } or { Checks.Count: > 0 })
                throw new InvalidOperationException("An INSERT can only be conditional on the row not existing (IF NOT EXISTS).");

            var props = GetMappedProperties(entity.GetType(), builder);
            var columns = props.Select(p => builder.GetColumn(p.Name)).ToList();
            var placeholders = Enumerable.Repeat("?", columns.Count);

            var values = new List<object?>(props.Select(p => ValueOf(p, entity, overrides, builder)));
            var (usingClause, usingValues) = BuildUsing(options, allowTtl: true, conditional: condition is not null);
            values.AddRange(usingValues);

            var cql = $"INSERT INTO {builder.GetFullTableName()} " +
                      $"({string.Join(", ", columns)}) " +
                      $"VALUES ({string.Join(", ", placeholders)})" +
                      (condition is { IfNotExists: true } ? " IF NOT EXISTS" : string.Empty) +
                      usingClause;

            return (cql, values.ToArray());
        }

        public (string cql, object?[] parameters) GenerateUpdate(object entity, IEntityBuilder builder, WriteOptions? options = null)
            => GenerateUpdate(entity, builder, options, excluded: null, cleared: null, overrides: null, condition: null);

        public (string cql, object?[] parameters) GenerateUpdate(object entity, IEntityBuilder builder, WriteOptions? options,
            WriteCondition? condition)
            => GenerateUpdate(entity, builder, options, excluded: null, cleared: null, overrides: null, condition);

        private (string cql, object?[] parameters) GenerateUpdate(
            object entity, IEntityBuilder builder, WriteOptions? options, IReadOnlySet<string>? excluded,
            IReadOnlySet<string>? cleared, IReadOnlyDictionary<string, object?>? overrides, WriteCondition? condition)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());
            if (condition is { IfNotExists: true })
                throw new InvalidOperationException("An UPDATE cannot be conditional on the row not existing; use an INSERT.");

            var props = GetMappedProperties(entity.GetType(), builder)
                .Where(p => !pkNames.Contains(p.Name) && excluded?.Contains(p.Name) != true)
                .ToList();

            // UPDATE needs at least one SET clause; for key-only entities an INSERT is the equivalent upsert.
            if (props.Count == 0)
                return condition is null
                    ? GenerateInsert(entity, builder, options)
                    : throw new InvalidOperationException($"{entity.GetType().Name} has only key columns: nothing to update conditionally.");

            var (usingClause, usingValues) = BuildUsing(options, allowTtl: true, conditional: condition is not null);

            // Bind markers are positional: USING comes before SET in the text.
            var values = new List<object?>(usingValues);
            values.AddRange(props.Select(p => cleared?.Contains(p.Name) == true ? null : ValueOf(p, entity, overrides, builder)));
            values.AddRange(GetPrimaryKeyValues(entity, pkNames, builder));

            var setClauses = props.Select(p => $"{builder.GetColumn(p.Name)} = ?");
            var cql = $"UPDATE {builder.GetFullTableName()}{usingClause} " +
                      $"SET {string.Join(", ", setClauses)} " +
                      $"WHERE {BuildWhereClause(builder, pkNames)}" +
                      IfClause(condition, builder, values);

            return (cql, values.ToArray());
        }

        public (string cql, object?[] parameters) GenerateDelete(object entity, IEntityBuilder builder, WriteOptions? options = null)
            => GenerateDelete(entity, builder, options, condition: null);

        public (string cql, object?[] parameters) GenerateDelete(object entity, IEntityBuilder builder, WriteOptions? options,
            WriteCondition? condition)
        {
            var pkNames = ValidatePrimaryKey(builder, entity.GetType());
            if (condition is { IfNotExists: true })
                throw new InvalidOperationException("A DELETE cannot be conditional on the row not existing.");
            var (usingClause, usingValues) = BuildUsing(options, allowTtl: false, conditional: condition is not null);

            var values = new List<object?>(usingValues);
            values.AddRange(GetPrimaryKeyValues(entity, pkNames, builder));

            var cql = $"DELETE FROM {builder.GetFullTableName()}{usingClause} WHERE {BuildWhereClause(builder, pkNames)}" +
                      IfClause(condition, builder, values);
            return (cql, values.ToArray());
        }

        private static readonly HashSet<string> ConditionOperators = ["=", "!=", "<", "<=", ">", ">="];

        // " IF EXISTS", " IF col = ? AND ...": the values go last, after WHERE's.
        private static string IfClause(WriteCondition? condition, IEntityBuilder builder, List<object?> values)
        {
            if (condition is null)
                return string.Empty;
            if (condition.IfExists && condition.Checks.Count == 0)
                return " IF EXISTS";
            if (condition.Checks.Count == 0)
                return string.Empty;

            var parts = condition.Checks.Select(check =>
            {
                if (!ConditionOperators.Contains(check.Operator))
                    throw new InvalidOperationException($"Unsupported condition operator '{check.Operator}'.");
                values.Add(builder.ToProvider(check.Property, check.Value));
                return $"{builder.GetColumn(check.Property)} {check.Operator} ?";
            });
            return $" IF {string.Join(" AND ", parts)}";
        }

        public (string cql, object?[] parameters) GenerateClearColumns(
            object entity, IEntityBuilder builder, IReadOnlyCollection<string> properties, WriteOptions? options = null)
        {
            if (properties.Count == 0)
                throw new ArgumentException("At least one column must be cleared.", nameof(properties));

            var pkNames = ValidatePrimaryKey(builder, entity.GetType());

            var keyColumns = properties.Where(pkNames.Contains).ToList();
            if (keyColumns.Count > 0)
                throw new InvalidOperationException(
                    $"Primary key columns cannot be cleared: {string.Join(", ", keyColumns)}. Delete the row instead.");

            var (usingClause, usingValues) = BuildUsing(options, allowTtl: false, conditional: false);

            var values = new List<object?>(usingValues);
            values.AddRange(GetPrimaryKeyValues(entity, pkNames, builder));

            var columns = properties.Select(builder.GetColumn);
            var cql = $"DELETE {string.Join(", ", columns)} FROM {builder.GetFullTableName()}{usingClause} " +
                      $"WHERE {BuildWhereClause(builder, pkNames)}";

            return (cql, values.ToArray());
        }

        private static (string clause, object?[] values) BuildUsing(WriteOptions? options, bool allowTtl, bool conditional)
        {
            if (options is null || options.IsEmpty)
                return (string.Empty, []);

            options.Validate();

            if (conditional && options.Timestamp is not null)
                throw new InvalidOperationException("A conditional write (lightweight transaction) cannot use a custom timestamp.");

            if (options.Ttl is not null && !allowTtl)
                throw new InvalidOperationException("USING TTL applies to inserts and updates only, not to deletes.");

            var parts = new List<string>();
            var values = new List<object?>();

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
        private static object ValueOf(PropertyInfo property, object entity, IReadOnlyDictionary<string, object?>? overrides,
            IEntityBuilder builder)
            => builder.ToProvider(property.Name,
                   overrides is not null && overrides.TryGetValue(property.Name, out var value) ? value : property.GetValue(entity))
               ?? Unset.Value;

        private static List<PropertyInfo> GetMappedProperties(Type type, IEntityBuilder builder) =>
            builder.GetMappedProperties()
                .Concat(builder.GetPrimaryKey())
                .Distinct()
                .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
                .OfType<PropertyInfo>()
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

        private static object[] GetPrimaryKeyValues(object entity, IReadOnlyList<string> pkNames, IEntityBuilder builder)
        {
            var type = entity.GetType();

            return pkNames.Select(pk =>
            {
                var prop = type.GetProperty(pk, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"Primary key property {pk} not found");

                return builder.ToProvider(pk, prop.GetValue(entity))
                    ?? throw new InvalidOperationException($"Primary key property {pk} of {type.Name} is null");
            }).ToArray();
        }

        private static string BuildWhereClause(IEntityBuilder builder, IReadOnlyList<string> pkNames) =>
            string.Join(" AND ", pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));
    }
}
