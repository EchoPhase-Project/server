using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;
using EchoPhase.Types.Repository;

// ── RefreshTokenRepository ────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class RefreshTokenRepository : RepositoryBase<RefreshToken>
    {
        private readonly PostgresContext _context;

        public RefreshTokenRepository(PostgresContext context)
        {
            _context = context;
        }

        public new RefreshTokenQuery Query() => new(Build());

        public override IQueryable<RefreshToken> Build()
            => _context.RefreshTokens;

        public override void Add(RefreshToken entity) => _context.RefreshTokens.Add(entity);
        public override void Update(RefreshToken entity) => _context.RefreshTokens.Update(entity);
        public override void Remove(RefreshToken entity) => _context.RefreshTokens.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
