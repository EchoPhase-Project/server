namespace EchoPhase.DAL.Scylla
{
    public class MigrationBuilder
    {
        private readonly List<string> _commands = new();

        public void CreateTable(string keyspace, string name, Action<TableBuilder> buildAction)
        {
            var tableBuilder = new TableBuilder(name, keyspace);
            buildAction(tableBuilder);
            _commands.Add(tableBuilder.Build());
        }

        public IReadOnlyList<string> GetCommands() => _commands.AsReadOnly();
    }
}
