using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class RefreshTokenRepository
        : RepositoryBase<RefreshToken, RefreshTokenOptions, RefreshTokenSearchOptions>
    {
        private readonly PostgresContext _dbContext;

        public RefreshTokenRepository(PostgresContext dbContext) : base()
        {
            _dbContext = dbContext;
        }

        public override IQueryable<RefreshToken> Build()
        {
            IQueryable<RefreshToken> query = _dbContext.RefreshTokens;
            if (_options.IncludeUser)
                query = query.Include(q => q.User);
            if (_options.IncludeClaims)
                query = query
                    .Include(q => q.Scopes)
                    .Include(q => q.Intents)
                    .Include(q => q.Permissions);
            return query;
        }

        protected override IQueryable<RefreshToken> ApplyExtraFilters(
            IQueryable<RefreshToken> query,
            RefreshTokenSearchOptions opts)
        {
            if (opts.Ids           is { Count: > 0 }) query = query.Where(x => opts.Ids.Contains(x.Id));
            if (opts.UserIds       is { Count: > 0 }) query = query.Where(x => opts.UserIds.Contains(x.UserId));
            if (opts.DeviceIds     is { Count: > 0 }) query = query.Where(x => x.DeviceId != null && opts.DeviceIds.Contains(x.DeviceId));
            if (opts.RefreshValues is { Count: > 0 }) query = query.Where(x => x.RefreshValue != null && opts.RefreshValues.Contains(x.RefreshValue));
            return query;
        }
    }
}
