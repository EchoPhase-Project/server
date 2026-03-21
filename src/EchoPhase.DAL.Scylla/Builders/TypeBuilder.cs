// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Builders
{
    public class TypeBuilder
    {
        private readonly string _name;
        private readonly string _keyspace;
        private readonly List<string> _fields = new();

        public TypeBuilder(string name, string keyspace)
        {
            _name = name;
            _keyspace = keyspace;
        }

        public TypeBuilder Field(string name, string type)
        {
            _fields.Add($"{name} {type}");
            return this;
        }

        public string Build()
        {
            var fields = string.Join(",\n    ", _fields);
            return $@"CREATE TYPE {_keyspace}.{_name} (
    {fields}
);".Trim();
        }
    }
}
