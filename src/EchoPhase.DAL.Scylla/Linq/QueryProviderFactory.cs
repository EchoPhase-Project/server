// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Linq
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
