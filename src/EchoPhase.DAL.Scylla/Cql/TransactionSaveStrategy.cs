using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Cql
{
    public class TransactionSaveStrategy : ISaveStrategy
    {
        private readonly ICqlGenerator _cqlGenerator;
        private readonly Func<Type, IEntityBuilder> _getBuilder;

        public TransactionSaveStrategy(ICqlGenerator cqlGenerator, Func<Type, IEntityBuilder> getBuilder)
        {
            _cqlGenerator = cqlGenerator;
            _getBuilder = getBuilder;
        }

        public int Execute(IEnumerable<TrackedEntity> entities, Transaction? transaction)
        {
            var count = 0;
            foreach (var tracked in entities)
            {
                var (cql, parameters) = GenerateCql(tracked);
                if (cql == null) continue;

                if (transaction != null)
                    transaction.AddStatement(cql, parameters);
                else
                    throw new InvalidOperationException("Transaction is required");

                count++;
            }
            return count;
        }

        public async Task<int> ExecuteAsync(IEnumerable<TrackedEntity> entities, Transaction? transaction)
        {
            return Execute(entities, transaction);
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
