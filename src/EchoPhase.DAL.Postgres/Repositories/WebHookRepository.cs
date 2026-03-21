// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Repository;

// ── WebHookRepository ─────────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class WebHookRepository : RepositoryBase<WebHook>
    {
        private readonly PostgresContext _context;
        private readonly IntentsBitMask _intentsBitmask;

        public WebHookRepository(PostgresContext context, IntentsBitMask intentsBitmask)
        {
            _context = context;
            _intentsBitmask = intentsBitmask;
        }

        public new WebHookQuery Query() => new(Build(), _intentsBitmask);

        public override IQueryable<WebHook> Build()
            => _context.WebHooks;

        public override void Add(WebHook entity) => _context.WebHooks.Add(entity);
        public override void Update(WebHook entity) => _context.WebHooks.Update(entity);
        public override void Remove(WebHook entity) => _context.WebHooks.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
