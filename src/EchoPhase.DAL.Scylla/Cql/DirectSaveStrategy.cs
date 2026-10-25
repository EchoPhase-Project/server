// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Cql
{
    /// <summary>
    /// Writes each change as its own statement. Plain INSERT/UPDATE/DELETE
    /// are idempotent, so the driver may retry them on timeouts; conditional
    /// ones (a concurrency token, an IF) are not, and report whether they
    /// applied: one that did not marks its entity conflicted.
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
                var builder = _getBuilder(tracked.EntityType);
                var conditional = Prepare(tracked, builder);
                var applied = true;
                foreach (var (cql, parameters) in _cqlGenerator.Generate(tracked, builder))
                    applied &= _queryExecutor.Run(new CqlStatement(cql, parameters, Idempotent: !conditional, Options: tracked.Options?.Statement)).Applied ?? true;
                count += Finish(tracked, builder, applied);
            }
            return count;
        }

        public async Task<int> ExecuteAsync(IEnumerable<TrackedEntity> entities, Batch? batch, CancellationToken ct = default)
        {
            var count = 0;
            foreach (var tracked in entities)
            {
                var builder = _getBuilder(tracked.EntityType);
                var conditional = Prepare(tracked, builder);
                var applied = true;
                foreach (var (cql, parameters) in _cqlGenerator.Generate(tracked, builder))
                    applied &= (await _queryExecutor.RunAsync(new CqlStatement(cql, parameters, Idempotent: !conditional, Options: tracked.Options?.Statement), null, ct)).Applied ?? true;
                count += Finish(tracked, builder, applied);
            }
            return count;
        }

        // Picks the token value this save writes; true when the write is conditional.
        internal static bool Prepare(TrackedEntity tracked, IEntityBuilder builder)
        {
            tracked.Conflicted = false;
            tracked.NewToken = null;
            if (builder.ConcurrencyToken is { } token && tracked.State is EntityState.Added or EntityState.Modified)
                tracked.NewToken = NextToken(tracked.EntityType.GetProperty(token)!.GetValue(tracked.Entity));
            return CqlGenerator.ConditionFor(tracked, builder) is not null;
        }

        // The entity takes its new token only when the write applied.
        private static int Finish(TrackedEntity tracked, IEntityBuilder builder, bool applied)
        {
            if (!applied)
            {
                tracked.Conflicted = true;
                return 0;
            }
            if (builder.ConcurrencyToken is { } token && tracked.NewToken is not null)
                tracked.EntityType.GetProperty(token)!.SetValue(tracked.Entity, tracked.NewToken);
            return 1;
        }

        internal static object NextToken(object? current) => current switch
        {
            int n => n + 1,
            long n => n + 1,
            _ => Guid.CreateVersion7(),
        };
    }
}
