// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using EchoPhase.DAL.Scylla.Cql;

namespace EchoPhase.DAL.Scylla.Builders
{
    public class TableBuilder
    {
        private string _name;
        private string? _keyspace;
        private readonly bool _ifNotExists;
        private readonly List<string> _columns = new();
        private string? _primaryKey;
        private string? _clusteringOrder;
        private readonly Dictionary<string, string> _options = new();

        public TableBuilder(string name, string? keyspace = null, bool ifNotExists = false)
        {
            _name = name;
            _keyspace = keyspace;
            _ifNotExists = ifNotExists;
        }

        // Метод для указания keyspace
        public TableBuilder WithKeyspace(string keyspace)
        {
            _keyspace = keyspace;
            return this;
        }

        /// <summary>
        /// Sets the table name, for <c>MigrationBuilder.CreateTable(Action)</c>
        /// where it is not passed up front.
        /// </summary>
        public TableBuilder Named(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Table name cannot be empty.", nameof(name));

            _name = name;
            return this;
        }

        // CQL has no NOT NULL constraint: only primary key columns are implicitly non-null.
        public TableBuilder Column(string name, string type)
        {
            _columns.Add($"{name} {type}");
            return this;
        }

        /// <summary>Column whose CQL type is derived from <typeparamref name="T"/>.</summary>
        public TableBuilder Column<T>(string name)
            => Column(name, Schema.CqlTypeMap.For(typeof(T)));

        // Типизированные методы для распространенных типов
        public TableBuilder Int(string name)
            => Column(name, "int");

        public TableBuilder BigInt(string name)
            => Column(name, "bigint");

        public TableBuilder Text(string name)
            => Column(name, "text");

        public TableBuilder Varchar(string name)
            => Column(name, "varchar");

        public TableBuilder Uuid(string name)
            => Column(name, "uuid");

        public TableBuilder TimeUuid(string name)
            => Column(name, "timeuuid");

        public TableBuilder Timestamp(string name)
            => Column(name, "timestamp");

        public TableBuilder Boolean(string name)
            => Column(name, "boolean");

        public TableBuilder Decimal(string name)
            => Column(name, "decimal");

        public TableBuilder Double(string name)
            => Column(name, "double");

        public TableBuilder Float(string name)
            => Column(name, "float");

        public TableBuilder Blob(string name)
            => Column(name, "blob");

        public TableBuilder SmallInt(string name)
            => Column(name, "smallint");

        public TableBuilder TinyInt(string name)
            => Column(name, "tinyint");

        public TableBuilder VarInt(string name)
            => Column(name, "varint");

        public TableBuilder Counter(string name)
            => Column(name, "counter");

        public TableBuilder Inet(string name)
            => Column(name, "inet");

        public TableBuilder Date(string name)
            => Column(name, "date");

        public TableBuilder Time(string name)
            => Column(name, "time");

        public TableBuilder Duration(string name)
            => Column(name, "duration");

        public TableBuilder List(string name, string elementType)
            => Column(name, $"list<{elementType}>");

        public TableBuilder Set(string name, string elementType)
            => Column(name, $"set<{elementType}>");

        public TableBuilder Map(string name, string keyType, string valueType)
            => Column(name, $"map<{keyType}, {valueType}>");

        public TableBuilder Frozen(string name, string type)
            => Column(name, $"frozen<{type}>");

        // Primary Key методы
        public TableBuilder PrimaryKey(params string[] columns)
        {
            _primaryKey = $"PRIMARY KEY ({string.Join(", ", columns)})";
            return this;
        }

        public TableBuilder PrimaryKey(string partitionKey, params string[] clusteringColumns)
        {
            if (clusteringColumns.Length == 0)
            {
                _primaryKey = $"PRIMARY KEY ({partitionKey})";
            }
            else
            {
                _primaryKey = $"PRIMARY KEY (({partitionKey}), {string.Join(", ", clusteringColumns)})";
            }
            return this;
        }

        public TableBuilder CompositePrimaryKey(string[] partitionKeys, params string[] clusteringColumns)
        {
            var partitionPart = string.Join(", ", partitionKeys);
            if (clusteringColumns.Length == 0)
            {
                _primaryKey = $"PRIMARY KEY (({partitionPart}))";
            }
            else
            {
                _primaryKey = $"PRIMARY KEY (({partitionPart}), {string.Join(", ", clusteringColumns)})";
            }
            return this;
        }

        // Clustering Order
        public TableBuilder ClusterKey(string column, bool descending = false)
        {
            var order = descending ? "DESC" : "ASC";
            _clusteringOrder = $"WITH CLUSTERING ORDER BY ({column} {order})";
            return this;
        }

        public TableBuilder ClusterKeys(params (string column, bool descending)[] columns)
        {
            var orders = columns.Select(c => $"{c.column} {(c.descending ? "DESC" : "ASC")}");
            _clusteringOrder = $"WITH CLUSTERING ORDER BY ({string.Join(", ", orders)})";
            return this;
        }

        // Table Options
        public TableBuilder WithCompression(string compressionClass, params (string key, string value)[] options)
        {
            _options["compression"] = CqlLiteral.Options("sstable_compression", compressionClass, options);
            return this;
        }

        public TableBuilder WithCompaction(string compactionClass, params (string key, string value)[] options)
        {
            _options["compaction"] = CqlLiteral.Options("class", compactionClass, options);
            return this;
        }

        public TableBuilder WithGcGraceSeconds(int seconds)
        {
            _options["gc_grace_seconds"] = seconds.ToString();
            return this;
        }

        public TableBuilder WithBloomFilterFpChance(double chance)
        {
            _options["bloom_filter_fp_chance"] = chance.ToString(CultureInfo.InvariantCulture);
            return this;
        }

        public TableBuilder WithDefaultTtl(int ttl)
        {
            _options["default_time_to_live"] = ttl.ToString();
            return this;
        }

        public TableBuilder WithCaching(string keys, string rowsPerPartition)
        {
            _options["caching"] = $"{{'keys': {CqlLiteral.Quote(keys)}, 'rows_per_partition': {CqlLiteral.Quote(rowsPerPartition)}}}";
            return this;
        }

        public TableBuilder WithComment(string comment)
        {
            _options["comment"] = CqlLiteral.Quote(comment);
            return this;
        }

        public string Build()
        {
            if (string.IsNullOrEmpty(_keyspace))
                throw new InvalidOperationException("Keyspace must be specified for the table");

            if (string.IsNullOrWhiteSpace(_name))
                throw new InvalidOperationException("Table name must be specified (pass it to CreateTable or call Named)");

            var ifNotExistsClause = _ifNotExists ? "IF NOT EXISTS " : "";
            var cols = string.Join(",\n    ", _columns);
            var pk = _primaryKey != null ? $",\n    {_primaryKey}" : "";

            var withClauses = new List<string>();
            if (_clusteringOrder != null)
            {
                withClauses.Add(_clusteringOrder.Replace("WITH ", ""));
            }

            foreach (var option in _options)
            {
                withClauses.Add($"{option.Key} = {option.Value}");
            }

            var withClause = withClauses.Count > 0
                ? $"\nWITH {string.Join("\n AND ", withClauses)}"
                : "";

            return $@"CREATE TABLE {ifNotExistsClause}{_keyspace}.{_name} (
    {cols}{pk}
){withClause};".Trim();
        }
    }
}
