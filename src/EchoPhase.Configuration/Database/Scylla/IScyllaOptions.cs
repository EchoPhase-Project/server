// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Database.Scylla
{
    public interface IScyllaOptions
    {
        /// <summary>
        /// One or more hosts, comma-separated. The driver discovers the rest of
        /// the cluster from any of them.
        /// </summary>
        string ContactPoint
        {
            get;
        }

        int Port
        {
            get;
        }

        /// <summary>
        /// Datacenter to prefer for requests; empty lets the driver infer it
        /// from the contact points (fine for a single-DC cluster).
        /// </summary>
        string? LocalDatacenter
        {
            get;
        }

        string Keyspace
        {
            get;
        }

        string Username
        {
            get;
        }

        string Password
        {
            get;
        }

        /// <summary>
        /// Default consistency, a <c>Cassandra.ConsistencyLevel</c> name
        /// (e.g. <c>LocalOne</c>, <c>LocalQuorum</c>).
        /// </summary>
        string Consistency
        {
            get;
        }

        /// <summary>
        /// Consistency of the Paxos phase of lightweight transactions
        /// (<c>LocalSerial</c> or <c>Serial</c>).
        /// </summary>
        string SerialConsistency
        {
            get;
        }

        TimeSpan RequestTimeout
        {
            get;
        }

        /// <summary>
        /// Create the keyspace (NetworkTopologyStrategy, ReplicationFactor in
        /// every datacenter) when it does not exist. For single-node
        /// development only; production keyspaces need per-datacenter
        /// replication chosen deliberately.
        /// </summary>
        bool AutoCreateKeyspace
        {
            get;
        }

        /// <summary>Replication factor used by <see cref="AutoCreateKeyspace"/>.</summary>
        int ReplicationFactor
        {
            get;
        }
    }
}
