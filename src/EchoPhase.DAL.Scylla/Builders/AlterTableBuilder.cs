// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Builders
{
    public class AlterTableBuilder
    {
        private readonly string _name;
        private readonly string _keyspace;
        private readonly List<string> _alterations = new();

        public AlterTableBuilder(string name, string keyspace)
        {
            _name = name;
            _keyspace = keyspace;
        }

        public AlterTableBuilder AddColumn(string name, string type)
        {
            _alterations.Add($"ALTER TABLE {_keyspace}.{_name} ADD {name} {type};");
            return this;
        }

        public AlterTableBuilder DropColumn(string name)
        {
            _alterations.Add($"ALTER TABLE {_keyspace}.{_name} DROP {name};");
            return this;
        }

        public AlterTableBuilder RenameColumn(string oldName, string newName)
        {
            _alterations.Add($"ALTER TABLE {_keyspace}.{_name} RENAME {oldName} TO {newName};");
            return this;
        }

        public AlterTableBuilder AlterColumnType(string name, string newType)
        {
            _alterations.Add($"ALTER TABLE {_keyspace}.{_name} ALTER {name} TYPE {newType};");
            return this;
        }

        public AlterTableBuilder WithCompaction(string compactionClass, params (string key, string value)[] options)
        {
            var opts = options.Length > 0
                ? ", " + string.Join(", ", options.Select(o => $"'{o.key}': '{o.value}'"))
                : "";
            _alterations.Add($"ALTER TABLE {_keyspace}.{_name} WITH compaction = {{'class': '{compactionClass}'{opts}}};");
            return this;
        }

        public AlterTableBuilder WithGcGraceSeconds(int seconds)
        {
            _alterations.Add($"ALTER TABLE {_keyspace}.{_name} WITH gc_grace_seconds = {seconds};");
            return this;
        }

        public IReadOnlyList<string> Build() => _alterations.AsReadOnly();
    }
}
