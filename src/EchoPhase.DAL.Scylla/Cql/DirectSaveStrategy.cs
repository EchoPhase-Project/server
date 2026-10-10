// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Cql
{
    /// <summary>
    /// Writes each change as its own statement. Generated writes are plain
    /// INSERT/UPDATE/DELETE (no LWT, no counters), so they are idempotent and
    /// the driver may retry them on timeouts.
    /// </summary>
    public class DirectSaveStrategy : ISaveStrategy
    {
        private readonly ICqlGenerator _cqlGenerator;
        private readonly QueryExecutor _queryExecutor;
        private readonly Func<Type, IEntityBuilder> _getBuilder;

        public DirectSaveStrategy(
            ICqlGenerator cqlGenerator,
            QueryExecutor queryExecutor,
            Func<Type, IEntityBuilder> getBuilder)
        {
            _cqlGenerator = cqlGenerator;
            _queryExecutor = queryExecutor;
            _getBuilder = getBuilder;
        }

        public int Execute(IEnumerable<TrackedEntity> entities, Batch? batch)
        {
            var count = 0;

            foreach (var tracked in entities)
            {
                var statements = _cqlGenerator.Generate(tracked, _getBuilder(tracked.EntityType));
                if (statements.Count == 0) continue;

                foreach (var (cql, parameters) in statements)
                    _queryExecutor.ExecuteNonQuery(cql, parameters, idempotent: true);

                count++;
            }

            return count;
        }

        public async Task<int> ExecuteAsync(IEnumerable<TrackedEntity> entities, Batch? batch, CancellationToken ct = default)
        {
            var count = 0;

            foreach (var tracked in entities)
            {
                var statements = _cqlGenerator.Generate(tracked, _getBuilder(tracked.EntityType));
                if (statements.Count == 0) continue;

                foreach (var (cql, parameters) in statements)
                    await _queryExecutor.ExecuteNonQueryAsync(cql, parameters, idempotent: true, ct);

                count++;
            }

            return count;
        }
    }
}
