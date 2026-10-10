// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Builders;

namespace EchoPhase.DAL.Scylla.Tests
{
    public class KeyspaceBuilderTests
    {
        [Fact]
        public void CreateKeyspace_UsesNetworkTopologyWithAutoExpansion()
        {
            // Tablets (the Scylla default) reject SimpleStrategy.
            var migration = new MigrationBuilder();
            migration.CreateKeyspace("echophase", replicationFactor: 1);

            var cql = Assert.Single(migration.GetCommands());
            Assert.Contains("CREATE KEYSPACE IF NOT EXISTS echophase", cql);
            Assert.Contains("{'class': 'NetworkTopologyStrategy', 'replication_factor': 1}", cql);
            Assert.DoesNotContain("SimpleStrategy", cql);
        }
    }
}
