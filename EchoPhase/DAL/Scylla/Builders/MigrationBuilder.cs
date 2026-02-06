namespace EchoPhase.DAL.Scylla
{
    public class MigrationBuilder
    {
        private readonly List<string> _commands = new();
        private readonly string? _defaultKeyspace;

        public MigrationBuilder(string? defaultKeyspace = null)
        {
            _defaultKeyspace = defaultKeyspace;
        }

        #region Keyspace Operations

        public void CreateKeyspace(string name, Action<KeyspaceBuilder> buildAction)
        {
            var keyspaceBuilder = new KeyspaceBuilder(name);
            buildAction(keyspaceBuilder);
            _commands.Add(keyspaceBuilder.Build());
        }

        public void CreateKeyspace(string name, int replicationFactor = 1, bool ifNotExists = true, bool durableWrites = true)
        {
            CreateKeyspace(name, ks => ks
                .WithSimpleStrategy(replicationFactor)
                .WithDurableWrites(durableWrites)
                .IfNotExists(ifNotExists)
            );
        }

        public void CreateKeyspaceWithNetworkTopology(
            string name,
            Dictionary<string, int> datacenterReplication,
            bool ifNotExists = true,
            bool durableWrites = true)
        {
            CreateKeyspace(name, ks => ks
                .WithNetworkTopologyStrategy(datacenterReplication)
                .WithDurableWrites(durableWrites)
                .IfNotExists(ifNotExists)
            );
        }

        public void AlterKeyspace(string name, Action<KeyspaceBuilder> buildAction)
        {
            var keyspaceBuilder = new KeyspaceBuilder(name);
            buildAction(keyspaceBuilder);
            _commands.Add(keyspaceBuilder.BuildAlter());
        }

        public void DropKeyspace(string name, bool ifExists = true)
        {
            var keyspaceBuilder = new KeyspaceBuilder(name);
            _commands.Add(keyspaceBuilder.BuildDrop(ifExists));
        }

        public void UseKeyspace(string name)
        {
            _commands.Add($"USE {name};");
        }

        #endregion

        #region Table Operations

        public void CreateTable(string keyspace, string name, Action<TableBuilder> buildAction, bool ifNotExists = false)
        {
            var tableBuilder = new TableBuilder(name, keyspace, ifNotExists);
            buildAction(tableBuilder);
            _commands.Add(tableBuilder.Build());
        }

        public void CreateTable(string name, Action<TableBuilder> buildAction, bool ifNotExists = false)
        {
            if (_defaultKeyspace == null)
                throw new InvalidOperationException("Default keyspace is not set. Use CreateTable(keyspace, name, ...) or set default keyspace in constructor.");

            CreateTable(_defaultKeyspace, name, buildAction, ifNotExists);
        }

        public void CreateTable(Action<TableBuilder> buildAction, bool ifNotExists = false)
        {
            var tableBuilder = new TableBuilder("", _defaultKeyspace, ifNotExists);
            buildAction(tableBuilder);
            _commands.Add(tableBuilder.Build());
        }

        public void DropTable(string keyspace, string name, bool ifExists = true)
        {
            var ifExistsClause = ifExists ? "IF EXISTS " : "";
            _commands.Add($"DROP TABLE {ifExistsClause}{keyspace}.{name};");
        }

        public void DropTable(string name, bool ifExists = true)
        {
            if (_defaultKeyspace == null)
                throw new InvalidOperationException("Default keyspace is not set.");

            DropTable(_defaultKeyspace, name, ifExists);
        }

        public void AlterTable(string keyspace, string name, Action<AlterTableBuilder> alterAction)
        {
            var alterBuilder = new AlterTableBuilder(name, keyspace);
            alterAction(alterBuilder);
            _commands.AddRange(alterBuilder.Build());
        }

        public void AlterTable(string name, Action<AlterTableBuilder> alterAction)
        {
            if (_defaultKeyspace == null)
                throw new InvalidOperationException("Default keyspace is not set.");

            AlterTable(_defaultKeyspace, name, alterAction);
        }

        #endregion

        #region Index Operations

        public void CreateIndex(string keyspace, string tableName, string indexName, string columnName)
        {
            _commands.Add($"CREATE INDEX {indexName} ON {keyspace}.{tableName} ({columnName});");
        }

        public void CreateIndex(string tableName, string indexName, string columnName)
        {
            if (_defaultKeyspace == null)
                throw new InvalidOperationException("Default keyspace is not set.");

            CreateIndex(_defaultKeyspace, tableName, indexName, columnName);
        }

        public void DropIndex(string keyspace, string indexName, bool ifExists = true)
        {
            var ifExistsClause = ifExists ? "IF EXISTS " : "";
            _commands.Add($"DROP INDEX {ifExistsClause}{keyspace}.{indexName};");
        }

        public void DropIndex(string indexName, bool ifExists = true)
        {
            if (_defaultKeyspace == null)
                throw new InvalidOperationException("Default keyspace is not set.");

            DropIndex(_defaultKeyspace, indexName, ifExists);
        }

        #endregion

        #region Type Operations

        public void CreateType(string keyspace, string name, Action<TypeBuilder> buildAction)
        {
            var typeBuilder = new TypeBuilder(name, keyspace);
            buildAction(typeBuilder);
            _commands.Add(typeBuilder.Build());
        }

        public void CreateType(string name, Action<TypeBuilder> buildAction)
        {
            if (_defaultKeyspace == null)
                throw new InvalidOperationException("Default keyspace is not set.");

            CreateType(_defaultKeyspace, name, buildAction);
        }

        public void DropType(string keyspace, string name, bool ifExists = true)
        {
            var ifExistsClause = ifExists ? "IF EXISTS " : "";
            _commands.Add($"DROP TYPE {ifExistsClause}{keyspace}.{name};");
        }

        public void DropType(string name, bool ifExists = true)
        {
            if (_defaultKeyspace == null)
                throw new InvalidOperationException("Default keyspace is not set.");

            DropType(_defaultKeyspace, name, ifExists);
        }

        #endregion

        #region Raw SQL

        public void Sql(string sql)
        {
            _commands.Add(sql);
        }

        #endregion

        public IReadOnlyList<string> GetCommands() => _commands.AsReadOnly();
    }
}
