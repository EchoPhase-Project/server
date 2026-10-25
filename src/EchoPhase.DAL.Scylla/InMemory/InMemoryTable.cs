// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.InMemory
{
    /// <summary>A cell: its value (null: deleted), when it was written (microseconds) and when it expires.</summary>
    internal sealed record Cell(object? Value, long Timestamp, DateTimeOffset? Expires)
    {
        public bool IsLive(DateTimeOffset now, long shadowedUpTo)
            => Value is not null && Timestamp > shadowedUpTo && (Expires is null || Expires > now);
    }

    /// <summary>
    /// A row as Scylla keeps it: cells written at their own times, a marker
    /// from INSERT (so a row with only its key exists), a tombstone from DELETE.
    /// </summary>
    internal sealed class StoredRow
    {
        public object?[] Clustering { get; init; } = [];

        public Cell? Marker { get; set; }

        public long Tombstone { get; set; } = long.MinValue;

        public Dictionary<string, Cell> Cells { get; } = new(StringComparer.Ordinal);
    }

    internal sealed class Partition
    {
        public object?[] Key { get; init; } = [];

        public long Tombstone { get; set; } = long.MinValue;

        public SortedList<object?[], StoredRow> Rows { get; init; } = null!;
    }

    internal sealed class InMemoryTable
    {
        public InMemoryTable(string keyspace, string name, IEnumerable<ColumnDefinition> columns,
            IReadOnlyList<string> partitionKey, IReadOnlyList<string> clusteringKey,
            IReadOnlyDictionary<string, bool> descending, int? defaultTtl)
        {
            Keyspace = keyspace;
            Name = name;
            foreach (var column in columns)
                Columns[column.Name] = column.Type;
            PartitionKey = partitionKey;
            ClusteringKey = clusteringKey;
            Descending = clusteringKey.Select(c => descending.TryGetValue(c, out var d) && d).ToArray();
            DefaultTtl = defaultTtl;
            ClusteringComparer = Comparer<object?[]>.Create(CompareClustering);
        }

        public string Keyspace { get; }

        public string Name { get; set; }

        /// <summary>Every column, in the order it was defined.</summary>
        public Dictionary<string, CqlType> Columns { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<string> PartitionKey { get; }

        public IReadOnlyList<string> ClusteringKey { get; }

        public bool[] Descending { get; }

        public int? DefaultTtl { get; set; }

        public HashSet<string> Indexed { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Partition> Partitions { get; } = new(StringComparer.Ordinal);

        public IComparer<object?[]> ClusteringComparer { get; }

        public bool IsKey(string column) => PartitionKey.Contains(column) || ClusteringKey.Contains(column);

        public CqlType TypeOf(string column)
            => Columns.TryGetValue(column, out var type)
                ? type
                : throw new Cassandra.InvalidQueryException($"Undefined column name {column} in table {Keyspace}.{Name}");

        /// <summary>Columns as SELECT * returns them: partition key, clustering key, then the rest by name.</summary>
        public IEnumerable<string> StarColumns
            => PartitionKey.Concat(ClusteringKey)
                .Concat(Columns.Keys.Where(c => !IsKey(c)).OrderBy(c => c, StringComparer.Ordinal));

        public string PartitionId(object?[] key)
            => string.Join('\u001f', key.Select((v, i) => Format(TypeOf(PartitionKey[i]), v)));

        private static string Format(CqlType type, object? value) => value switch
        {
            null => "\u0000",
            byte[] bytes => Convert.ToHexString(bytes),
            DateTimeOffset moment => moment.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        private int CompareClustering(object?[] a, object?[] b)
        {
            for (var i = 0; i < ClusteringKey.Count; i++)
            {
                var c = TypeOf(ClusteringKey[i]).Compare(a[i], b[i]);
                if (c != 0)
                    return Descending[i] ? -c : c;
            }
            return 0;
        }
    }
}
