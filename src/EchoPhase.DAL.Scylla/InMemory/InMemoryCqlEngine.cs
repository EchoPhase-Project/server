// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using Cassandra;
using EchoPhase.DAL.Scylla.Database;

namespace EchoPhase.DAL.Scylla.InMemory
{
    /// <summary>
    /// ScyllaDB in memory, for tests: runs the CQL the ORM sends (its migrations
    /// too) with Scylla's rules, so a query the cluster would refuse fails here
    /// as well. Rows keep cells written at their own times, with TTLs (on the
    /// given clock), tombstones and lightweight transactions; system_schema
    /// answers for the tables it holds.
    /// </summary>
    /// <remarks>
    /// One engine is one cluster: share it between contexts to share data.
    /// Thread-safe; every statement runs alone, as an LWT would on a replica.
    /// Not supported: user types, static columns, materialized views,
    /// conditional batches, deleting collection elements.
    /// </remarks>
    public sealed class InMemoryCqlEngine : ICqlEngine
    {
        private const string FilteringMessage =
            "Cannot execute this query as it might involve data filtering and thus may have unpredictable performance. " +
            "If you want to execute this query despite the performance unpredictability, use ALLOW FILTERING";

        private static readonly ConcurrentDictionary<string, (Statement Statement, int Markers)> Parsed = new();

        private readonly object _sync = new();
        private readonly Dictionary<string, InMemoryTable> _tables = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (InMemoryTable Table, string Column)> _indexes = new(StringComparer.Ordinal);
        private readonly string _keyspace;
        private readonly TimeProvider _time;
        private long _lastTimestamp;

        /// <param name="keyspace">The keyspace of names without one (the connection's).</param>
        /// <param name="time">The clock of TTLs and write timestamps.</param>
        public InMemoryCqlEngine(string keyspace = "test", TimeProvider? time = null)
        {
            _keyspace = keyspace.ToLowerInvariant();
            _time = time ?? TimeProvider.System;
        }

        /// <summary>Statements run so far (text only), to assert on in tests.</summary>
        public ConcurrentQueue<string> Log { get; } = new();

        public CqlResult Execute(CqlStatement statement)
        {
            Log.Enqueue(statement.Cql);
            var parsed = Parsed.GetOrAdd(statement.Cql, CqlParser.Parse);
            lock (_sync)
                return Run(parsed, Values(statement), statement);
        }

        public Task<CqlResult> ExecuteAsync(CqlStatement statement, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(statement));
        }

        public void ExecuteBatch(IReadOnlyList<CqlStatement> statements, BatchType type)
        {
            var parsed = statements.Select(s => (Parsed: Parsed.GetOrAdd(s.Cql, CqlParser.Parse), Source: s))
                .Select(p => (p.Parsed, p.Parsed.Statement, p.Source)).ToList();
            if (parsed.Any(p => p.Statement is Insert { IfNotExists: true } or Update { If.Any: true } or Delete { If.Any: true }))
                throw new NotSupportedException("Conditional batches are not supported by the in-memory engine.");
            if (parsed.Any(p => p.Statement is not (Insert or Update or Delete)))
                throw new InvalidQueryException("Only INSERT, UPDATE and DELETE statements are allowed in a batch.");

            lock (_sync)
            {
                // One timestamp for the whole batch, as the coordinator gives it.
                var timestamp = NextTimestamp();
                foreach (var (statement, _, source) in parsed.Select(p => (p.Parsed, p.Statement, p.Source)))
                {
                    Log.Enqueue(source.Cql);
                    Run(statement, Values(source), source, timestamp);
                }
            }
        }

        public Task ExecuteBatchAsync(IReadOnlyList<CqlStatement> statements, BatchType type, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ExecuteBatch(statements, type);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        /// <summary>Forgets every table and row.</summary>
        public void Reset()
        {
            lock (_sync)
            {
                _tables.Clear();
                _indexes.Clear();
            }
        }

        private static object?[] Values(CqlStatement statement) => statement.Values ?? [];

        private DateTimeOffset Now => _time.GetUtcNow();

        // Microseconds, strictly increasing: writes in one test never tie.
        private long NextTimestamp()
        {
            var now = (Now - DateTimeOffset.UnixEpoch).Ticks / 10;
            _lastTimestamp = Math.Max(now, _lastTimestamp + 1);
            return _lastTimestamp;
        }

        #region Dispatch

        private CqlResult Run((Statement Statement, int Markers) parsed, object?[] values, CqlStatement source,
            long? batchTimestamp = null)
        {
            if (values.Length != parsed.Markers)
                throw new InvalidQueryException(
                    $"Invalid amount of bind variables: expected {parsed.Markers}, got {values.Length}");
            var statement = parsed.Statement;
            var bind = new Binder(values);
            var result = statement switch
            {
                NoOp => CqlResult.Empty,
                CreateTable create => RunCreateTable(create),
                AlterTable alter => RunAlterTable(alter),
                DropTable drop => RunDropTable(drop),
                CreateIndex index => RunCreateIndex(index),
                DropIndex index => RunDropIndex(index),
                Insert insert => RunInsert(insert, bind, batchTimestamp),
                Update update => RunUpdate(update, bind, batchTimestamp),
                Delete delete => RunDelete(delete, bind, batchTimestamp),
                Select select => RunSelect(select, bind, source),
                _ => throw new NotSupportedException($"Unsupported statement {statement.GetType().Name}."),
            };
            return result;
        }

        private sealed class Binder
        {
            private readonly object?[] _values;

            public Binder(object?[] values) => _values = values;

            public object? Raw(Term term) => term switch
            {
                Marker m => Marker(m.Index),
                Literal l => l.Value,
                ListLiteral list => list.Items.Select(Raw).ToList(),
                SetLiteral set => set.Items.Select(Raw).ToList(),
                MapLiteral map => map.Pairs.ToDictionary(p => Raw(p.Key)!, p => Raw(p.Value)),
                _ => throw new NotSupportedException(),
            };

            public object? Value(Term term, CqlType type)
                => Raw(term) is Unset ? throw new InvalidQueryException("Invalid unset value") : type.Normalize(Raw(term));

            private object? Marker(int index) => _values[index];

        }

        private InMemoryTable Table(TableName name)
        {
            var key = Key(name);
            return _tables.TryGetValue(key, out var table)
                ? table
                : throw new InvalidQueryException($"unconfigured table {name.Name}");
        }

        private string Key(TableName name) => $"{name.Keyspace ?? _keyspace}.{name.Name}";

        #endregion

        #region DDL

        private CqlResult RunCreateTable(CreateTable create)
        {
            var key = Key(create.Table);
            if (_tables.ContainsKey(key))
                return create.IfNotExists ? CqlResult.Empty : throw new InvalidQueryException($"Table {key} already exists");

            _tables[key] = new InMemoryTable(create.Table.Keyspace ?? _keyspace, create.Table.Name, create.Columns,
                create.PartitionKey, create.ClusteringKey, create.Descending, create.DefaultTtl);
            return CqlResult.Empty;
        }

        private CqlResult RunAlterTable(AlterTable alter)
        {
            var table = Table(alter.Table);
            switch (alter.Action)
            {
                case "add":
                    if (table.Columns.ContainsKey(alter.Column!))
                        throw new InvalidQueryException($"Invalid column name {alter.Column} because it conflicts with an existing column");
                    table.Columns[alter.Column!] = alter.Type!;
                    break;
                case "drop":
                    if (table.IsKey(alter.Column!))
                        throw new InvalidQueryException($"Cannot drop PRIMARY KEY part {alter.Column}");
                    if (!table.Columns.Remove(alter.Column!))
                        throw new InvalidQueryException($"Column {alter.Column} was not found in table {table.Name}");
                    foreach (var partition in table.Partitions.Values)
                        foreach (var row in partition.Rows.Values)
                            row.Cells.Remove(alter.Column!);
                    break;
                case "rename":
                    throw new NotSupportedException("Renaming columns is not supported by the in-memory engine.");
                case "ttl":
                    table.DefaultTtl = int.Parse(alter.Column!, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "truncate":
                    table.Partitions.Clear();
                    break;
            }
            return CqlResult.Empty;
        }

        private CqlResult RunDropTable(DropTable drop)
        {
            var key = Key(drop.Table);
            if (!_tables.Remove(key, out var table))
                return drop.IfExists ? CqlResult.Empty : throw new InvalidQueryException($"unconfigured table {drop.Table.Name}");
            foreach (var index in _indexes.Where(i => i.Value.Table == table).Select(i => i.Key).ToList())
                _indexes.Remove(index);
            return CqlResult.Empty;
        }

        private CqlResult RunCreateIndex(CreateIndex create)
        {
            var table = Table(create.Table);
            table.TypeOf(create.Column);
            var name = create.Name ?? $"{table.Name}_{create.Column}_idx";
            var key = $"{table.Keyspace}.{name}";
            if (_indexes.ContainsKey(key))
                return create.IfNotExists ? CqlResult.Empty : throw new InvalidQueryException($"Index {name} already exists");
            _indexes[key] = (table, create.Column);
            table.Indexed.Add(create.Column);
            return CqlResult.Empty;
        }

        private CqlResult RunDropIndex(DropIndex drop)
        {
            var key = Key(drop.Index);
            if (!_indexes.Remove(key, out var index))
                return drop.IfExists ? CqlResult.Empty : throw new InvalidQueryException($"Index '{drop.Index.Name}' could not be found");
            index.Table.Indexed.Remove(index.Column);
            return CqlResult.Empty;
        }

        #endregion

        #region Writes

        private (long Timestamp, DateTimeOffset? Expires) WriteTime(InMemoryTable table, WriteUsing usingClause, Binder bind,
            long? batchTimestamp, bool isDelete = false, bool conditional = false)
        {
            if (conditional && usingClause.Timestamp is not null)
                throw new InvalidQueryException("Cannot provide custom timestamp for conditional updates");
            if (isDelete && usingClause.Ttl is not null)
                throw new InvalidQueryException("TTL attribute is not allowed for deletes");

            var timestamp = usingClause.Timestamp is { } ts
                ? Convert.ToInt64(bind.Raw(ts), System.Globalization.CultureInfo.InvariantCulture)
                : batchTimestamp ?? NextTimestamp();

            var ttl = usingClause.Ttl is { } t
                ? Convert.ToInt32(bind.Raw(t), System.Globalization.CultureInfo.InvariantCulture)
                : table.DefaultTtl ?? 0;
            if (ttl < 0)
                throw new InvalidQueryException("A TTL must be greater or equal to 0");
            return (timestamp, ttl > 0 ? Now.AddSeconds(ttl) : null);
        }

        private CqlResult RunInsert(Insert insert, Binder bind, long? batchTimestamp)
        {
            var table = Table(insert.Table);
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < insert.Columns.Count; i++)
            {
                var column = insert.Columns[i];
                var type = table.TypeOf(column);
                // Unset: the value is left as it is (the driver's "not bound").
                if (bind.Raw(insert.Values[i]) is Unset)
                    continue;
                if (type.IsCounter)
                    throw new InvalidQueryException("INSERT statements are not allowed on counter tables, use UPDATE instead");
                if (!values.TryAdd(column, bind.Value(insert.Values[i], type)))
                    throw new InvalidQueryException($"Multiple definitions found for column {column}");
            }

            var missingPartition = table.PartitionKey.Where(c => !values.TryGetValue(c, out var v) || v is null).ToList();
            if (missingPartition.Count > 0)
                throw new InvalidQueryException($"Some partition key parts are missing: {string.Join(", ", missingPartition)}");
            var missingClustering = table.ClusteringKey.Where(c => !values.TryGetValue(c, out var v) || v is null).ToList();
            if (missingClustering.Count > 0)
                throw new InvalidQueryException($"Some clustering keys are missing: {string.Join(", ", missingClustering)}");

            var (timestamp, expires) = WriteTime(table, insert.Using, bind, batchTimestamp, conditional: insert.IfNotExists);
            var partitionKey = table.PartitionKey.Select(c => values[c]).ToArray();
            var clustering = table.ClusteringKey.Select(c => values[c]).ToArray();

            if (insert.IfNotExists)
            {
                var existing = LiveRow(table, partitionKey, clustering);
                if (existing is not null)
                    return Applied(false, table, existing, table.StarColumns);
            }

            var row = RowFor(table, partitionKey, clustering, create: true)!;
            Write(ref row, Cell(true, timestamp, expires), isMarker: true);
            foreach (var (column, value) in values.Where(v => !table.IsKey(v.Key)))
                WriteCell(row, column, new Cell(value, timestamp, value is null ? null : expires));

            return insert.IfNotExists ? Applied(true) : CqlResult.Empty;
        }

        private static Cell Cell(object? value, long timestamp, DateTimeOffset? expires) => new(value, timestamp, expires);

        private static void Write(ref StoredRow row, Cell marker, bool isMarker)
        {
            if (isMarker && (row.Marker is null || marker.Timestamp >= row.Marker.Timestamp))
                row.Marker = marker;
        }

        // Last write wins, by timestamp.
        private static void WriteCell(StoredRow row, string column, Cell cell)
        {
            if (!row.Cells.TryGetValue(column, out var current) || cell.Timestamp >= current.Timestamp)
                row.Cells[column] = cell;
        }

        private CqlResult RunUpdate(Update update, Binder bind, long? batchTimestamp)
        {
            var table = Table(update.Table);
            var (timestamp, expires) = WriteTime(table, update.Using, bind, batchTimestamp, conditional: update.If.Any);

            // Values are bound in text order: SET comes before WHERE.
            var assignments = update.Assignments.Where(a => bind.Raw(a.Value) is not Unset).Select(a =>
            {
                if (table.IsKey(a.Column))
                    throw new InvalidQueryException($"PRIMARY KEY part {a.Column} found in SET part");
                var type = table.TypeOf(a.Column);
                var key = a.Key is null ? null : bind.Raw(a.Key);
                object? value = a.Kind switch
                {
                    "set" => bind.Value(a.Value, type),
                    "element" => bind.Raw(a.Value),
                    "remove" when type.Unfrozen.Name == "map" => bind.Raw(a.Value),
                    _ when type.IsCounter => bind.Value(a.Value, CqlType.Of("bigint")),
                    _ => bind.Value(a.Value, type),
                };
                if (type.IsCounter && a.Kind is not ("add" or "remove"))
                    throw new InvalidQueryException($"Cannot set the value of counter column {a.Column} (counters can only be incremented/decremented, not set)");
                return (Assignment: a, Type: type, Value: value, Key: key);
            }).ToList();

            var targets = Targets(table, update.Where, bind, requireFullClustering: true);
            if (update.If.Any)
            {
                if (targets.Count != 1)
                    throw new InvalidQueryException("IN on the clustering key columns is not supported with conditional updates");
                var (pk, ck) = targets[0];
                var check = Check(table, update.If, bind, pk, ck);
                if (check is not null)
                    return check;
            }

            foreach (var (pk, ck) in targets)
            {
                var row = RowFor(table, pk, ck, create: true)!;
                var shadow = Math.Max(row.Tombstone, Partition(table, pk)!.Tombstone);
                foreach (var (assignment, type, value, key) in assignments)
                {
                    row.Cells.TryGetValue(assignment.Column, out var current);
                    var currentValue = current is not null && current.IsLive(Now, shadow) ? current.Value : null;
                    var next = Assign(type, assignment, currentValue, value, key);
                    WriteCell(row, assignment.Column, new Cell(next, timestamp, next is null || type.IsCounter ? null : expires));
                }
            }

            return update.If.Any ? Applied(true) : CqlResult.Empty;
        }

        private static object? Assign(CqlType type, Assignment assignment, object? current, object? value, object? key)
        {
            var t = type.Unfrozen;
            switch (assignment.Kind)
            {
                case "set":
                    return value;
                case "add" when type.IsCounter:
                    return (long)(current ?? 0L) + (long)value!;
                case "remove" when type.IsCounter:
                    return (long)(current ?? 0L) - (long)value!;
                case "add" or "prepend" when t.Name == "list":
                    var items = ((Array?)current)?.Cast<object?>() ?? [];
                    var added = ((Array)value!).Cast<object?>();
                    return t.Normalize(assignment.Kind == "add" ? items.Concat(added).ToList() : added.Concat(items).ToList());
                case "add" when t.Name == "set":
                    return t.Normalize((((Array?)current)?.Cast<object?>() ?? []).Concat(((Array)value!).Cast<object?>()).ToList());
                case "add" when t.Name == "map":
                    var merged = new Dictionary<object, object?>();
                    foreach (System.Collections.DictionaryEntry e in (System.Collections.IDictionary?)current ?? new Dictionary<object, object?>())
                        merged[e.Key] = e.Value;
                    foreach (System.Collections.DictionaryEntry e in (System.Collections.IDictionary)value!)
                        merged[e.Key] = e.Value;
                    return t.Normalize(merged);
                case "remove" when t.Name is "list" or "set":
                    var removed = ((Array)value!).Cast<object?>().ToList();
                    var kept = (((Array?)current)?.Cast<object?>() ?? [])
                        .Where(i => !removed.Any(r => t.Arguments[0].Compare(r, i) == 0)).ToList();
                    return kept.Count == 0 ? null : t.Normalize(kept);
                case "remove" when t.Name == "map":
                    var keys = ((System.Collections.IEnumerable)value!).Cast<object?>().Select(t.Arguments[0].Normalize).ToList();
                    var rest = new Dictionary<object, object?>();
                    foreach (System.Collections.DictionaryEntry e in (System.Collections.IDictionary?)current ?? new Dictionary<object, object?>())
                        if (!keys.Any(k => t.Arguments[0].Compare(k, e.Key) == 0))
                            rest[e.Key] = e.Value;
                    return rest.Count == 0 ? null : t.Normalize(rest);
                case "element" when t.Name == "map":
                    var map = new Dictionary<object, object?>();
                    foreach (System.Collections.DictionaryEntry e in (System.Collections.IDictionary?)current ?? new Dictionary<object, object?>())
                        map[e.Key] = e.Value;
                    map[t.Arguments[0].Normalize(key)!] = t.Arguments[1].Normalize(value);
                    return t.Normalize(map);
                case "element" when t.Name == "list":
                    var list = ((Array?)current)?.Cast<object?>().ToList() ?? [];
                    var index = Convert.ToInt32(key, System.Globalization.CultureInfo.InvariantCulture);
                    if (index < 0 || index >= list.Count)
                        throw new InvalidQueryException($"List index {index} out of bound, list has size {list.Count}");
                    list[index] = t.Arguments[0].Normalize(value);
                    return t.Normalize(list);
                default:
                    throw new InvalidQueryException($"Invalid operation ({assignment.Kind}) for column {assignment.Column} of type {type}");
            }
        }

        private CqlResult RunDelete(Delete delete, Binder bind, long? batchTimestamp)
        {
            var table = Table(delete.Table);
            foreach (var column in delete.Columns)
                if (table.IsKey(column))
                    throw new InvalidQueryException($"Invalid identifier {column} for deletion (should not be a PRIMARY KEY part)");
                else
                    table.TypeOf(column);

            var (timestamp, _) = WriteTime(table, delete.Using, bind, batchTimestamp, isDelete: true, conditional: delete.If.Any);
            var partitions = PartitionKeys(table, delete.Where, bind);
            var clusteringRelations = delete.Where.Where(r => table.ClusteringKey.Contains(r.Column)).ToList();
            foreach (var relation in delete.Where.Where(r => !table.IsKey(r.Column)))
                throw new InvalidQueryException($"Non PRIMARY KEY columns found in where clause: {relation.Column}");

            var fullClustering = table.ClusteringKey.All(c => clusteringRelations.Any(r => r.Column == c && r.Op == Operator.Eq));
            if (delete.Columns.Count > 0 && !fullClustering)
                throw new InvalidQueryException("Range deletions are not supported for specific columns");

            if (delete.If.Any)
            {
                if (partitions.Count != 1 || !fullClustering)
                    throw new InvalidQueryException("DELETE statements must restrict all PRIMARY KEY columns with equality relations in order to use IF conditions");
                var ck = table.ClusteringKey.Select(c => bind.Value(clusteringRelations.First(r => r.Column == c).Value, table.TypeOf(c))).ToArray();
                var check = Check(table, delete.If, bind, partitions[0], ck);
                if (check is not null)
                    return check;
            }

            var clusteringValues = clusteringRelations.Select(r => (Relation: r, Value: Bound(table, r, bind))).ToList();
            foreach (var pk in partitions)
            {
                var partition = Partition(table, pk, create: true)!;
                if (clusteringRelations.Count == 0 && delete.Columns.Count == 0)
                {
                    partition.Tombstone = Math.Max(partition.Tombstone, timestamp);
                    continue;
                }

                if (fullClustering)
                {
                    var ck = table.ClusteringKey.Select(c => clusteringValues.First(v => v.Relation.Column == c).Value).ToArray();
                    var row = RowFor(table, pk, ck, create: true)!;
                    if (delete.Columns.Count == 0)
                        row.Tombstone = Math.Max(row.Tombstone, timestamp);
                    else
                        foreach (var column in delete.Columns)
                            WriteCell(row, column, new Cell(null, timestamp, null));
                    continue;
                }

                foreach (var row in partition.Rows.Values)
                {
                    var values = table.ClusteringKey.Select((c, i) => (c, row.Clustering[i]))
                        .ToDictionary(x => x.c, x => x.Item2, StringComparer.Ordinal);
                    if (clusteringValues.All(v => Matches(table, v.Relation.Op, values[v.Relation.Column], v.Value, v.Relation.Column)))
                        row.Tombstone = Math.Max(row.Tombstone, timestamp);
                }
            }

            return delete.If.Any ? Applied(true) : CqlResult.Empty;
        }

        // The rows an UPDATE writes: every partition key part and clustering column by = or IN.
        private List<(object?[] Pk, object?[] Ck)> Targets(InMemoryTable table, IReadOnlyList<Relation> where, Binder bind,
            bool requireFullClustering)
        {
            foreach (var relation in where.Where(r => !table.IsKey(r.Column)))
            {
                table.TypeOf(relation.Column);
                throw new InvalidQueryException($"Non PRIMARY KEY columns found in where clause: {relation.Column}");
            }

            var partitions = PartitionKeys(table, where, bind);
            var choices = new List<List<object?>>();
            foreach (var column in table.ClusteringKey)
            {
                var relation = where.FirstOrDefault(r => r.Column == column);
                if (relation is null || relation.Op is not (Operator.Eq or Operator.In))
                    throw new InvalidQueryException($"Missing mandatory PRIMARY KEY part {column}");
                choices.Add(KeyValues(table, relation, bind));
            }

            var clusterings = Cartesian(choices);
            return partitions.SelectMany(pk => clusterings.Select(ck => (pk, ck))).ToList();
        }

        private List<object?[]> PartitionKeys(InMemoryTable table, IReadOnlyList<Relation> where, Binder bind)
        {
            var choices = new List<List<object?>>();
            var missing = new List<string>();
            foreach (var column in table.PartitionKey)
            {
                var relation = where.FirstOrDefault(r => r.Column == column);
                if (relation is null)
                {
                    missing.Add(column);
                    continue;
                }
                if (relation.Op is not (Operator.Eq or Operator.In))
                    throw new InvalidQueryException($"Only EQ and IN relation are supported on the partition key (unless you use the token() function) for {column}");
                choices.Add(KeyValues(table, relation, bind));
            }
            if (missing.Count > 0)
                throw new InvalidQueryException($"Some partition key parts are missing: {string.Join(", ", missing)}");
            return Cartesian(choices);
        }

        private static List<object?> KeyValues(InMemoryTable table, Relation relation, Binder bind)
        {
            var type = table.TypeOf(relation.Column);
            var values = relation.Op == Operator.In
                ? ((System.Collections.IEnumerable)(bind.Raw(relation.Value) ?? Array.Empty<object>())).Cast<object?>().Select(type.Normalize).ToList()
                : [bind.Value(relation.Value, type)];
            if (values.Any(v => v is null))
                throw new InvalidQueryException($"Invalid null value in condition for column {relation.Column}");
            return values;
        }

        private static List<object?[]> Cartesian(List<List<object?>> choices)
        {
            IEnumerable<object?[]> result = [[]];
            foreach (var options in choices)
                result = result.SelectMany(prefix => options.Select(o => prefix.Append(o).ToArray()));
            return result.ToList();
        }

        // Null when the condition holds; otherwise the not-applied result.
        private CqlResult? Check(InMemoryTable table, Condition condition, Binder bind, object?[] pk, object?[] ck)
        {
            var live = LiveRow(table, pk, ck);
            if (condition.IfExists)
                return live is null ? Applied(false) : null;
            if (condition.IfNotExists)
                return live is null ? null : Applied(false, table, live, table.StarColumns);

            var holds = condition.Relations.All(r =>
            {
                var type = table.TypeOf(r.Column);
                var current = live?.GetValueOrDefault(r.Column);
                return r.Op switch
                {
                    Operator.Ne => type.Compare(current, bind.Value(r.Value, type)) != 0,
                    Operator.In => KeyValuesAllowNull(type, bind.Raw(r.Value)).Any(v => type.Compare(current, v) == 0),
                    _ => current is null
                        ? r.Op == Operator.Eq && bind.Value(r.Value, type) is null
                        : Matches(table, r.Op, current, bind.Value(r.Value, type), r.Column),
                };
            });
            return holds ? null : Applied(false, table, live, condition.Relations.Select(r => r.Column).Distinct());
        }

        private static IEnumerable<object?> KeyValuesAllowNull(CqlType type, object? values)
            => ((System.Collections.IEnumerable)(values ?? Array.Empty<object>())).Cast<object?>().Select(type.Normalize);

        private static CqlResult Applied(bool applied, InMemoryTable? table = null, Dictionary<string, object?>? row = null,
            IEnumerable<string>? columns = null)
        {
            var names = new List<RowFactory.Column> { new("[applied]", CqlType.Of("boolean")) };
            var values = new List<object?> { applied };
            if (!applied && table is not null && row is not null)
            {
                foreach (var column in columns ?? [])
                {
                    names.Add(new(column, table.TypeOf(column), table.Keyspace, table.Name));
                    values.Add(row.GetValueOrDefault(column));
                }
            }
            return new CqlResult([RowFactory.Create(RowFactory.Columns(names), values.ToArray())], null);
        }

        #endregion

        #region Rows

        private static Partition? Partition(InMemoryTable table, object?[] key, bool create = false)
        {
            var id = table.PartitionId(key);
            if (table.Partitions.TryGetValue(id, out var partition))
                return partition;
            if (!create)
                return null;
            partition = new Partition { Key = key, Rows = new SortedList<object?[], StoredRow>(table.ClusteringComparer) };
            table.Partitions[id] = partition;
            return partition;
        }

        private static StoredRow? RowFor(InMemoryTable table, object?[] pk, object?[] ck, bool create)
        {
            var partition = Partition(table, pk, create);
            if (partition is null)
                return null;
            if (partition.Rows.TryGetValue(ck, out var row))
                return row;
            if (!create)
                return null;
            row = new StoredRow { Clustering = ck };
            partition.Rows[ck] = row;
            return row;
        }

        private Dictionary<string, object?>? LiveRow(InMemoryTable table, object?[] pk, object?[] ck)
        {
            var partition = Partition(table, pk);
            return partition is not null && partition.Rows.TryGetValue(ck, out var row) ? Materialize(table, partition, row) : null;
        }

        // The row as a query sees it now, or null when nothing of it is live.
        private Dictionary<string, object?>? Materialize(InMemoryTable table, Partition partition, StoredRow row)
        {
            var now = Now;
            var shadow = Math.Max(partition.Tombstone, row.Tombstone);
            var cells = row.Cells.Where(c => c.Value.IsLive(now, shadow)).ToList();
            var markerLive = row.Marker is not null && row.Marker.IsLive(now, shadow);
            if (!markerLive && cells.Count == 0)
                return null;

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < table.PartitionKey.Count; i++)
                values[table.PartitionKey[i]] = partition.Key[i];
            for (var i = 0; i < table.ClusteringKey.Count; i++)
                values[table.ClusteringKey[i]] = row.Clustering[i];
            foreach (var (column, cell) in cells)
                values[column] = cell.Value;
            return values;
        }

        #endregion

        #region Reads

        private CqlResult RunSelect(Select select, Binder bind, CqlStatement source)
        {
            if ((select.Table.Keyspace ?? string.Empty) == "system_schema")
                return SystemSchema(select, bind);

            var table = Table(select.Table);
            foreach (var relation in select.Where)
                table.TypeOf(relation.Column);
            ValidateRestrictions(table, select);

            var bound = select.Where.Select(r => (Relation: r, Value: Bound(table, r, bind))).ToList();
            var limit = select.Limit is null ? (int?)null : Convert.ToInt32(bind.Raw(select.Limit), System.Globalization.CultureInfo.InvariantCulture);
            if (limit <= 0)
                throw new InvalidQueryException("LIMIT must be strictly positive");

            var reversed = select.OrderBy.Count > 0 && select.OrderBy[0].Descending != table.Descending[table.ClusteringKey.ToList().IndexOf(select.OrderBy[0].Column)];

            IEnumerable<Partition> partitions;
            if (table.PartitionKey.All(c => select.Where.Any(r => r.Column == c && r.Op is Operator.Eq or Operator.In)))
                partitions = Cartesian(table.PartitionKey.Select(c => KeyValuesFrom(bound, c)).ToList())
                    .Select(pk => Partition(table, pk)).OfType<Partition>();
            else
                partitions = table.Partitions.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value);

            var rows = new List<Dictionary<string, object?>>();
            foreach (var partition in partitions)
            {
                var ordered = reversed ? partition.Rows.Values.Reverse() : partition.Rows.Values;
                foreach (var stored in ordered)
                {
                    var row = Materialize(table, partition, stored);
                    if (row is not null && bound.All(b => Matches(table, b.Relation.Op, row.GetValueOrDefault(b.Relation.Column), b.Value, b.Relation.Column)))
                        rows.Add(row);
                }
            }

            if (select.Count)
            {
                var count = (long)(limit is { } l ? Math.Min(l, rows.Count) : rows.Count);
                return new CqlResult([RowFactory.Create(RowFactory.Columns([new("count", CqlType.Of("bigint"))]), [count])], null);
            }

            if (limit is { } max)
                rows = rows.Take(max).ToList();

            var columns = (select.Columns ?? table.StarColumns.ToList()).ToList();
            foreach (var column in columns)
                table.TypeOf(column);
            var cqlColumns = RowFactory.Columns(columns.Select(c => new RowFactory.Column(c, table.TypeOf(c), table.Keyspace, table.Name)).ToList());

            var start = source.PagingState is { Length: 4 } state ? BitConverter.ToInt32(state) : 0;
            var take = source.PageSize ?? int.MaxValue;
            var page = rows.Skip(start).Take(take).Select(r => RowFactory.Create(cqlColumns, columns.Select(c => r.GetValueOrDefault(c)).ToArray())).ToList();
            var next = source.PageSize is not null && start + page.Count < rows.Count ? BitConverter.GetBytes(start + page.Count) : null;
            return new CqlResult(page, next);
        }

        private static List<object?> KeyValuesFrom(List<(Relation Relation, object? Value)> bound, string column)
        {
            var (relation, value) = bound.First(b => b.Relation.Column == column && b.Relation.Op is Operator.Eq or Operator.In);
            return relation.Op == Operator.In ? ((IEnumerable<object?>)value!).ToList() : [value];
        }

        // A relation's value, typed: IN gets a list, CONTAINS the element (or key) type.
        private static object? Bound(InMemoryTable table, Relation relation, Binder bind)
        {
            var type = table.TypeOf(relation.Column);
            return relation.Op switch
            {
                Operator.In => KeyValuesAllowNull(type, bind.Raw(relation.Value)).ToList(),
                Operator.Contains => type.Unfrozen.Name == "map"
                    ? type.Unfrozen.Arguments[1].Normalize(bind.Raw(relation.Value))
                    : type.Unfrozen.Arguments[0].Normalize(bind.Raw(relation.Value)),
                Operator.ContainsKey => type.Unfrozen.Arguments[0].Normalize(bind.Raw(relation.Value)),
                _ => bind.Value(relation.Value, type),
            };
        }

        private static bool Matches(InMemoryTable table, Operator op, object? actual, object? expected, string column)
        {
            var type = table.TypeOf(column);
            if (actual is null)
                return false;
            switch (op)
            {
                case Operator.In:
                    return ((IEnumerable<object?>)expected!).Any(v => type.Compare(actual, v) == 0);
                case Operator.Contains:
                    var t = type.Unfrozen;
                    return t.Name == "map"
                        ? ((System.Collections.IDictionary)actual).Values.Cast<object?>().Any(v => t.Arguments[1].Compare(v, expected) == 0)
                        : ((Array)actual).Cast<object?>().Any(v => t.Arguments[0].Compare(v, expected) == 0);
                case Operator.ContainsKey:
                    return ((System.Collections.IDictionary)actual).Keys.Cast<object?>().Any(k => type.Unfrozen.Arguments[0].Compare(k, expected) == 0);
            }
            var c = type.Compare(actual, expected);
            return op switch
            {
                Operator.Eq => c == 0,
                Operator.Lt => c < 0,
                Operator.Le => c <= 0,
                Operator.Gt => c > 0,
                Operator.Ge => c >= 0,
                Operator.Ne => c != 0,
                _ => false,
            };
        }

        /// <summary>Scylla's rules for what a SELECT may restrict and order by without ALLOW FILTERING.</summary>
        private static void ValidateRestrictions(InMemoryTable table, Select select)
        {
            var where = select.Where;
            if (where.Any(r => r.Op == Operator.Ne))
                throw new InvalidQueryException("Unsupported \"!=\" relation");

            var filtering = false;
            var anyPartition = table.PartitionKey.Any(c => where.Any(r => r.Column == c));
            var fullPartition = table.PartitionKey.All(c => where.Any(r => r.Column == c && r.Op is Operator.Eq or Operator.In));
            if (anyPartition && !fullPartition)
                filtering = true;
            if (where.Any(r => table.PartitionKey.Contains(r.Column) && r.Op is not (Operator.Eq or Operator.In)))
                filtering = true;

            var gap = false;
            foreach (var column in table.ClusteringKey)
            {
                var relations = where.Where(r => r.Column == column).ToList();
                if (relations.Count == 0)
                {
                    gap = true;
                    continue;
                }
                if (gap || relations.Any(r => r.Op is Operator.Contains or Operator.ContainsKey))
                    filtering = true;
                if (!relations.Any(r => r.Op is Operator.Eq or Operator.In))
                    gap = true; // a range ends the usable prefix
            }
            if (!fullPartition && where.Any(r => table.ClusteringKey.Contains(r.Column)))
                filtering = true;

            var regular = where.Where(r => !table.IsKey(r.Column)).ToList();
            var indexed = regular.Count(r => r.Op == Operator.Eq && table.Indexed.Contains(r.Column));
            if (regular.Count > indexed || indexed > 1 || (indexed == 1 && regular.Count > 1))
                filtering = true;
            if (indexed == 1 && filtering && !select.AllowFiltering)
                throw new InvalidQueryException(FilteringMessage);

            if (filtering && !select.AllowFiltering)
                throw new InvalidQueryException(FilteringMessage);

            if (select.OrderBy.Count > 0)
            {
                if (!table.PartitionKey.All(c => where.Any(r => r.Column == c && r.Op is Operator.Eq or Operator.In)))
                    throw new InvalidQueryException("ORDER BY is only supported when the partition key is restricted by an EQ or an IN.");
                for (var i = 0; i < select.OrderBy.Count; i++)
                {
                    if (i >= table.ClusteringKey.Count || select.OrderBy[i].Column != table.ClusteringKey[i])
                        throw new InvalidQueryException("Order by currently only supports the ordering of columns following their declared order in the PRIMARY KEY");
                }
                var flips = select.OrderBy.Select((o, i) => o.Descending != table.Descending[i]).Distinct().Count();
                if (flips > 1)
                    throw new InvalidQueryException("Unsupported order by relation");
            }
        }

        private CqlResult SystemSchema(Select select, Binder bind)
        {
            string? Wanted(string column)
            {
                var relation = select.Where.FirstOrDefault(r => r.Column == column && r.Op == Operator.Eq);
                return relation is null ? null : bind.Raw(relation.Value) as string;
            }

            var keyspace = Wanted("keyspace_name");
            var tableName = Wanted("table_name");
            var tables = _tables.Values
                .Where(t => (keyspace is null || t.Keyspace == keyspace) && (tableName is null || t.Name == tableName))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            var text = CqlType.Of("text");
            List<(string Name, CqlType Type)> schema;
            List<object?[]> rows;
            switch (select.Table.Name)
            {
                case "tables":
                    schema = [("keyspace_name", text), ("table_name", text)];
                    rows = tables.Select(t => new object?[] { t.Keyspace, t.Name }).ToList();
                    break;
                case "columns":
                    schema = [("keyspace_name", text), ("table_name", text), ("column_name", text), ("kind", text),
                        ("position", CqlType.Of("int")), ("type", text), ("clustering_order", text)];
                    rows = tables.SelectMany(t => t.Columns.Select(c =>
                    {
                        var pk = t.PartitionKey.ToList().IndexOf(c.Key);
                        var ck = t.ClusteringKey.ToList().IndexOf(c.Key);
                        var kind = pk >= 0 ? "partition_key" : ck >= 0 ? "clustering" : "regular";
                        var order = ck >= 0 ? (t.Descending[ck] ? "desc" : "asc") : "none";
                        return new object?[] { t.Keyspace, t.Name, c.Key, kind, pk >= 0 ? pk : ck >= 0 ? ck : -1, c.Value.ToString(), order };
                    })).ToList();
                    break;
                default:
                    throw new InvalidQueryException($"unconfigured table {select.Table.Name}");
            }

            var columnName = Wanted("column_name");
            if (columnName is not null)
                rows = rows.Where(r => (string?)r[2] == columnName).ToList();

            var selected = select.Columns ?? schema.Select(s => s.Name).ToList();
            var indexes = selected.Select(c => schema.FindIndex(s => s.Name == c) is var i and >= 0
                ? i
                : throw new InvalidQueryException($"Undefined column name {c}")).ToList();
            var cqlColumns = RowFactory.Columns(indexes.Select(i => new RowFactory.Column(schema[i].Name, schema[i].Type, "system_schema", select.Table.Name)).ToList());
            if (select.Count)
                return new CqlResult([RowFactory.Create(RowFactory.Columns([new("count", CqlType.Of("bigint"))]), [(long)rows.Count])], null);
            return new CqlResult(rows.Select(r => RowFactory.Create(cqlColumns, indexes.Select(i => r[i]).ToArray())).ToList(), null);
        }

        #endregion
    }
}
