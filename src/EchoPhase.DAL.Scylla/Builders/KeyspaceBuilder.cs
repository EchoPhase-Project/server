// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Builders
{
    public class KeyspaceBuilder
    {
        private readonly string _name;
        private string _replicationClass = "SimpleStrategy";
        private int _replicationFactor = 1;
        private Dictionary<string, int>? _datacenterReplication;
        private bool _durableWrites = true;
        private bool _ifNotExists = true;

        public KeyspaceBuilder(string name)
        {
            _name = name;
        }

        #region Replication Strategy

        public KeyspaceBuilder WithSimpleStrategy(int replicationFactor)
        {
            _replicationClass = "SimpleStrategy";
            _replicationFactor = replicationFactor;
            _datacenterReplication = null;
            return this;
        }

        public KeyspaceBuilder WithNetworkTopologyStrategy(Dictionary<string, int> datacenterReplication)
        {
            _replicationClass = "NetworkTopologyStrategy";
            _datacenterReplication = datacenterReplication;
            return this;
        }

        public KeyspaceBuilder WithNetworkTopologyStrategy(params (string datacenter, int replicationFactor)[] datacenters)
        {
            _replicationClass = "NetworkTopologyStrategy";
            _datacenterReplication = datacenters.ToDictionary(dc => dc.datacenter, dc => dc.replicationFactor);
            return this;
        }

        public KeyspaceBuilder AddDatacenter(string datacenter, int replicationFactor)
        {
            if (_replicationClass != "NetworkTopologyStrategy")
            {
                _replicationClass = "NetworkTopologyStrategy";
                _datacenterReplication = new Dictionary<string, int>();
            }

            _datacenterReplication ??= new Dictionary<string, int>();
            _datacenterReplication[datacenter] = replicationFactor;
            return this;
        }

        #endregion

        #region Options

        public KeyspaceBuilder WithDurableWrites(bool enabled)
        {
            _durableWrites = enabled;
            return this;
        }

        public KeyspaceBuilder IfNotExists(bool value = true)
        {
            _ifNotExists = value;
            return this;
        }

        #endregion

        #region Build Methods

        public string Build()
        {
            var ifNotExistsClause = _ifNotExists ? "IF NOT EXISTS " : "";
            var durableWritesClause = _durableWrites ? "true" : "false";

            string replication;
            if (_datacenterReplication != null)
            {
                var dcReplicas = string.Join(", ", _datacenterReplication.Select(kvp => $"'{kvp.Key}': {kvp.Value}"));
                replication = $"{{'class': 'NetworkTopologyStrategy', {dcReplicas}}}";
            }
            else
            {
                replication = $"{{'class': '{_replicationClass}', 'replication_factor': {_replicationFactor}}}";
            }

            return $@"CREATE KEYSPACE {ifNotExistsClause}{_name}
    WITH replication = {replication}
    AND durable_writes = {durableWritesClause};";
        }

        public string BuildAlter()
        {
            var durableWritesClause = _durableWrites ? "true" : "false";

            string replication;
            if (_datacenterReplication != null)
            {
                var dcReplicas = string.Join(", ", _datacenterReplication.Select(kvp => $"'{kvp.Key}': {kvp.Value}"));
                replication = $"{{'class': 'NetworkTopologyStrategy', {dcReplicas}}}";
            }
            else
            {
                replication = $"{{'class': '{_replicationClass}', 'replication_factor': {_replicationFactor}}}";
            }

            return $@"ALTER KEYSPACE {_name}
    WITH replication = {replication}
    AND durable_writes = {durableWritesClause};";
        }

        public string BuildDrop(bool ifExists = true)
        {
            var ifExistsClause = ifExists ? "IF EXISTS " : "";
            return $"DROP KEYSPACE {ifExistsClause}{_name};";
        }

        #endregion

        #region Helper Methods

        public KeyspaceBuilder Clone()
        {
            return new KeyspaceBuilder(_name)
            {
                _replicationClass = _replicationClass,
                _replicationFactor = _replicationFactor,
                _datacenterReplication = _datacenterReplication != null
                    ? new Dictionary<string, int>(_datacenterReplication)
                    : null,
                _durableWrites = _durableWrites,
                _ifNotExists = _ifNotExists
            };
        }

        public override string ToString() => Build();

        #endregion
    }
}
