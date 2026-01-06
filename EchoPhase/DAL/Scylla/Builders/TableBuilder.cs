namespace EchoPhase.DAL.Scylla
{
    public class TableBuilder
    {
        private readonly string _name;
        private readonly string _keyspace;
        private readonly List<string> _columns = new();
        private string? _primaryKey;
        private string? _clusteringOrder;

        public TableBuilder(string name, string keyspace)
        {
            _name = name;
            _keyspace = keyspace;
        }

        public TableBuilder Column<T>(string name, string type, bool nullable = true)
        {
            var nullStr = nullable ? "" : " NOT NULL";
            _columns.Add($"{name} {type}{nullStr}");
            return this;
        }

        public TableBuilder PrimaryKey(params string[] columns)
        {
            _primaryKey = $"PRIMARY KEY ({string.Join(", ", columns)})";
            return this;
        }

        public TableBuilder ClusterKey(string column, bool descending = false)
        {
            var order = descending ? "DESC" : "ASC";
            _clusteringOrder = $"WITH CLUSTERING ORDER BY ({column} {order})";
            return this;
        }

        public string Build()
        {
            var cols = string.Join(",\n    ", _columns);
            var pk = _primaryKey != null ? $",\n    {_primaryKey}" : "";
            var cluster = _clusteringOrder != null ? $"\n{_clusteringOrder}" : "";

            return $@"
    CREATE TABLE {_keyspace}.{_name} (
        {cols}{pk}
    ){cluster};".Trim();
        }
    }
}
