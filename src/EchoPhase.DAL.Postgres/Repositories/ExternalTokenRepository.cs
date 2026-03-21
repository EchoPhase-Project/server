// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;
using EchoPhase.Security.Cryptography;
using EchoPhase.Types.Repository;

// ── ExternalTokenRepository ───────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class ExternalTokenRepository : RepositoryBase<ExternalToken>
    {
        private readonly PostgresContext _context;
        private readonly AesGcm _aes;

        public ExternalTokenRepository(PostgresContext context, AesGcm aes)
        {
            _context = context;
            _aes = aes;
        }

        public new ExternalTokenQuery Query() => new(Build(), _aes);

        public override IQueryable<ExternalToken> Build()
            => _context.ExternalTokens;

        public override async Task<int> Set(
            ExternalToken entity,
            Action<ExternalToken>? configure = null,
            CancellationToken ct = default)
        {
            configure?.Invoke(entity);
            entity.Value = _aes.Encrypt(entity.Value);
            return await base.Set(entity, ct: ct);
        }

        public override void Add(ExternalToken entity) => _context.ExternalTokens.Add(entity);
        public override void Update(ExternalToken entity) => _context.ExternalTokens.Update(entity);
        public override void Remove(ExternalToken entity) => _context.ExternalTokens.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
