using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla
{
    public class QueryProviderFactory : IQueryProviderFactory
    {
        private readonly DbContext _dbContext;

        public QueryProviderFactory(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public QueryProvider Create()
            => new QueryProvider(_dbContext);
    }
}
