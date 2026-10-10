// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class SessionRepository : RepositoryBase<Session>
    {
        private readonly PostgresContext _context;

        public SessionRepository(PostgresContext context)
        {
            _context = context;
        }

        public new SessionQuery Query() => new(Build());

        public override IQueryable<Session> Build()
            => _context.Sessions;

        public override void Add(Session entity) => _context.Sessions.Add(entity);
        public override void Update(Session entity) => _context.Sessions.Update(entity);
        public override void Remove(Session entity) => _context.Sessions.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
