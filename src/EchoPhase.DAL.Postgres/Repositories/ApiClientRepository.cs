// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class ApiClientRepository : RepositoryBase<ApiClient>
    {
        private readonly PostgresContext _context;

        public ApiClientRepository(PostgresContext context)
        {
            _context = context;
        }

        public new ApiClientQuery Query() => new(Build());

        public override IQueryable<ApiClient> Build()
            => _context.ApiClients;

        public override void Add(ApiClient entity) => _context.ApiClients.Add(entity);
        public override void Update(ApiClient entity) => _context.ApiClients.Update(entity);
        public override void Remove(ApiClient entity) => _context.ApiClients.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
