// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Schema
{
    /// <summary>A difference between the model and the live schema.</summary>
    /// <param name="Column">Null for table-level problems.</param>
    public sealed record SchemaProblem(string Table, string? Column, string Message)
    {
        public override string ToString() => Column is null ? $"{Table}: {Message}" : $"{Table}.{Column}: {Message}";
    }

    /// <summary>
    /// Checks configured entities against system_schema: the table exists,
    /// every mapped column exists with a compatible type, and the key layout
    /// (partition and clustering columns, in order) matches the model.
    /// </summary>
    internal sealed class SchemaValidator
    {
        private const string ColumnsQuery =
            "SELECT column_name, kind, position, type FROM system_schema.columns " +
            "WHERE keyspace_name = ? AND table_name = ?";

        private readonly QueryExecutor _executor;
        private readonly string _defaultKeyspace;

        public SchemaValidator(QueryExecutor executor, string defaultKeyspace)
        {
            _executor = executor;
            _defaultKeyspace = defaultKeyspace;
        }

        private sealed record LiveColumn(string Kind, int Position, string Type);

        public async Task<IReadOnlyList<SchemaProblem>> ValidateAsync(
            IEnumerable<IEntityBuilder> builders, CancellationToken ct)
        {
            var problems = new List<SchemaProblem>();

            foreach (var builder in builders)
                problems.AddRange(await ValidateAsync(builder, ct));

            return problems;
        }

        private async Task<List<SchemaProblem>> ValidateAsync(IEntityBuilder builder, CancellationToken ct)
        {
            var keyspace = builder.Keyspace ?? _defaultKeyspace;
            var table = builder.GetTableName();
            var label = $"{keyspace}.{table}";
            var problems = new List<SchemaProblem>();

            var rows = await _executor.ExecuteAsync(
                ColumnsQuery,
                row => (Name: row.GetValue<string>("column_name"),
                        Column: new LiveColumn(
                            row.GetValue<string>("kind"),
                            row.GetValue<int>("position"),
                            row.GetValue<string>("type"))),
                [keyspace, table],
                idempotent: true,
                ct);

            var live = rows.ToDictionary(r => r.Name, r => r.Column, StringComparer.Ordinal);
            if (live.Count == 0)
            {
                problems.Add(new SchemaProblem(label, null, "table does not exist"));
                return problems;
            }

            foreach (var property in builder.GetMappedProperties())
            {
                var column = builder.GetColumn(property);
                if (!live.TryGetValue(column, out var liveColumn))
                {
                    problems.Add(new SchemaProblem(label, column, "column does not exist"));
                    continue;
                }

                var clrType = builder.GetColumnType(property)
                              ?? builder.EntityType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.PropertyType;

                if (clrType is not null && !CqlTypeMap.IsCompatible(clrType, liveColumn.Type))
                    problems.Add(new SchemaProblem(label, column,
                        $"type {liveColumn.Type} cannot be read into {clrType.Name}"));
            }

            CheckKey(builder, builder.GetPartitionKey(), "partition_key", live, label, problems);
            CheckKey(builder, builder.GetClusteringKey(), "clustering", live, label, problems);

            return problems;
        }

        private static void CheckKey(
            IEntityBuilder builder,
            IReadOnlyList<string> properties,
            string kind,
            Dictionary<string, LiveColumn> live,
            string label,
            List<SchemaProblem> problems)
        {
            var expected = properties.Select(builder.GetColumn).ToList();
            var actual = live
                .Where(c => c.Value.Kind == kind)
                .OrderBy(c => c.Value.Position)
                .Select(c => c.Key)
                .ToList();

            if (!expected.SequenceEqual(actual))
                problems.Add(new SchemaProblem(label, null,
                    $"{kind.Replace('_', ' ')} columns are ({string.Join(", ", actual)}), model expects ({string.Join(", ", expected)})"));
        }
    }
}
