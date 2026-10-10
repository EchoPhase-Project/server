// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;

namespace EchoPhase.DAL.Postgres.Repositories
{
    /// <summary>
    /// Stores secrets exactly as given; encryption is the caller's concern so
    /// tracked entities never hold plain text.
    /// </summary>
    public class OAuthAppRepository : RepositoryBase<OAuthApp>
    {
        private readonly PostgresContext _context;

        public OAuthAppRepository(PostgresContext context)
        {
            _context = context;
        }

        public new OAuthAppQuery Query() => new(Build());

        public override IQueryable<OAuthApp> Build()
            => _context.OAuthApps;

        public override void Add(OAuthApp entity) => _context.OAuthApps.Add(entity);
        public override void Update(OAuthApp entity) => _context.OAuthApps.Update(entity);
        public override void Remove(OAuthApp entity) => _context.OAuthApps.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
