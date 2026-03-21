// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Cql
{
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

        public int Execute(IEnumerable<TrackedEntity> entities, Transaction? transaction)
        {
            var count = 0;
            foreach (var tracked in entities)
            {
                var (cql, parameters) = GenerateCql(tracked);
                if (cql == null) continue;

                _queryExecutor.ExecuteNonQuery(cql, parameters);
                count++;
            }
            return count;
        }

        public async Task<int> ExecuteAsync(IEnumerable<TrackedEntity> entities, Transaction? transaction)
        {
            var count = 0;
            foreach (var tracked in entities)
            {
                var (cql, parameters) = GenerateCql(tracked);
                if (cql == null) continue;

                await _queryExecutor.ExecuteNonQueryAsync(cql, parameters);
                count++;
            }
            return count;
        }

        private (string? cql, object[] parameters) GenerateCql(TrackedEntity tracked)
        {
            var builder = _getBuilder(tracked.EntityType);

            return tracked.State switch
            {
                EntityState.Added => _cqlGenerator.GenerateInsert(tracked.Entity, builder),
                EntityState.Modified => _cqlGenerator.GenerateUpdate(tracked.Entity, builder),
                EntityState.Deleted => _cqlGenerator.GenerateDelete(tracked.Entity, builder),
                _ => (null, Array.Empty<object>())
            };
        }
    }
}
