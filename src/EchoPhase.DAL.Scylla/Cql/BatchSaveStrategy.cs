// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Cql
{
    /// <summary>
    /// Queues changes in the active <see cref="Batch"/>; nothing is sent until
    /// the batch is committed.
    /// </summary>
    public class BatchSaveStrategy : ISaveStrategy
    {
        private readonly ICqlGenerator _cqlGenerator;
        private readonly Func<Type, IEntityBuilder> _getBuilder;

        public BatchSaveStrategy(ICqlGenerator cqlGenerator, Func<Type, IEntityBuilder> getBuilder)
        {
            _cqlGenerator = cqlGenerator;
            _getBuilder = getBuilder;
        }

        public int Execute(IEnumerable<TrackedEntity> entities, Batch? batch)
        {
            if (batch is null)
                throw new InvalidOperationException("A batch is required");

            var count = 0;

            foreach (var tracked in entities)
            {
                var statements = _cqlGenerator.Generate(tracked, _getBuilder(tracked.EntityType));
                if (statements.Count == 0) continue;

                foreach (var (cql, parameters) in statements)
                    batch.AddStatement(cql, parameters, idempotent: true);

                count++;
            }

            return count;
        }

        // Only queues statements in memory; there is nothing to await or cancel.
        public Task<int> ExecuteAsync(IEnumerable<TrackedEntity> entities, Batch? batch, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(entities, batch));
        }
    }
}
