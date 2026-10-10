// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class ImpersonationRepository : RepositoryBase<Impersonation>
    {
        private readonly PostgresContext _context;

        public ImpersonationRepository(PostgresContext context)
        {
            _context = context;
        }

        public new ImpersonationQuery Query() => new(Build());

        public override IQueryable<Impersonation> Build()
            => _context.Impersonations;

        public override void Add(Impersonation entity) => _context.Impersonations.Add(entity);
        public override void Update(Impersonation entity) => _context.Impersonations.Update(entity);
        public override void Remove(Impersonation entity) => _context.Impersonations.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
