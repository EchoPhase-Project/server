namespace EchoPhase.DAL.Scylla
{
    public class TableBuilder
    {
        private readonly string _name;
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

        public TableBuilder Column(string name, string type, bool nullable = true)
        {
            var nullStr = nullable ? "" : " NOT NULL";
            _columns.Add($"{name} {type}{nullStr}");
            return this;
        }

        public TableBuilder Column<T>(string name, string type, bool nullable = true)
        {
            return Column(name, type, nullable);
        }

        // Типизированные методы для распространенных типов
        public TableBuilder Int(string name, bool nullable = true)
            => Column(name, "int", nullable);

        public TableBuilder BigInt(string name, bool nullable = true)
            => Column(name, "bigint", nullable);

        public TableBuilder Text(string name, bool nullable = true)
            => Column(name, "text", nullable);

        public TableBuilder Varchar(string name, int? maxLength = null, bool nullable = true)
        {
            var type = maxLength.HasValue ? $"varchar({maxLength})" : "varchar";
            return Column(name, type, nullable);
        }

        public TableBuilder Uuid(string name, bool nullable = true)
            => Column(name, "uuid", nullable);

        public TableBuilder TimeUuid(string name, bool nullable = true)
            => Column(name, "timeuuid", nullable);

        public TableBuilder Timestamp(string name, bool nullable = true)
            => Column(name, "timestamp", nullable);

        public TableBuilder Boolean(string name, bool nullable = true)
            => Column(name, "boolean", nullable);

        public TableBuilder Decimal(string name, bool nullable = true)
            => Column(name, "decimal", nullable);

        public TableBuilder Double(string name, bool nullable = true)
            => Column(name, "double", nullable);

        public TableBuilder Float(string name, bool nullable = true)
            => Column(name, "float", nullable);

        public TableBuilder Blob(string name, bool nullable = true)
            => Column(name, "blob", nullable);

        public TableBuilder List(string name, string elementType, bool nullable = true)
            => Column(name, $"list<{elementType}>", nullable);

        public TableBuilder Set(string name, string elementType, bool nullable = true)
            => Column(name, $"set<{elementType}>", nullable);

        public TableBuilder Map(string name, string keyType, string valueType, bool nullable = true)
            => Column(name, $"map<{keyType}, {valueType}>", nullable);

        public TableBuilder Frozen(string name, string type, bool nullable = true)
            => Column(name, $"frozen<{type}>", nullable);

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
            var opts = options.Length > 0
                ? ", " + string.Join(", ", options.Select(o => $"'{o.key}': '{o.value}'"))
                : "";
            _options["compression"] = $"{{'class': '{compressionClass}'{opts}}}";
            return this;
        }

        public TableBuilder WithCompaction(string compactionClass, params (string key, string value)[] options)
        {
            var opts = options.Length > 0
                ? ", " + string.Join(", ", options.Select(o => $"'{o.key}': '{o.value}'"))
                : "";
            _options["compaction"] = $"{{'class': '{compactionClass}'{opts}}}";
            return this;
        }

        public TableBuilder WithGcGraceSeconds(int seconds)
        {
            _options["gc_grace_seconds"] = seconds.ToString();
            return this;
        }

        public TableBuilder WithBloomFilterFpChance(double chance)
        {
            _options["bloom_filter_fp_chance"] = chance.ToString("F2");
            return this;
        }

        public TableBuilder WithDefaultTtl(int ttl)
        {
            _options["default_time_to_live"] = ttl.ToString();
            return this;
        }

        public TableBuilder WithCaching(string keys, string rowsPerPartition)
        {
            _options["caching"] = $"{{'keys': '{keys}', 'rows_per_partition': '{rowsPerPartition}'}}";
            return this;
        }

        public TableBuilder WithComment(string comment)
        {
            _options["comment"] = $"'{comment}'";
            return this;
        }

        public string Build()
        {
            if (string.IsNullOrEmpty(_keyspace))
                throw new InvalidOperationException("Keyspace must be specified for the table");

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
