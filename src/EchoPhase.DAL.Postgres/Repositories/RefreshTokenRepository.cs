using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class RefreshTokenRepository : RepositoryBase<RefreshToken, RefreshTokenOptions>, IRefreshTokenRepository
    {
        private readonly PostgresContext _dbContext;

        public RefreshTokenRepository(PostgresContext dbContext) : base()
        {
            _dbContext = dbContext;
        }

        public IEnumerable<RefreshToken> Get(
            RefreshTokenSearchOptions options,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null
        )
        {
            var query = ApplySearchOptions<RefreshToken, RefreshTokenSearchOptions>(
                Build(), options, (query, opts) =>
                {
                    query = ExtraFilters(query, opts);
                    if (extraFilters != null)
                        query = extraFilters(query, opts);
                    return query;
                });
            return query;
        }

        public IEnumerable<RefreshToken> Get(
            Action<RefreshTokenSearchOptions> configure,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null
        )
        {
            var options = new RefreshTokenSearchOptions();
            configure(options);
            return Get(options, extraFilters);
        }

        public override IQueryable<RefreshToken> Build()
        {
            IQueryable<RefreshToken> query = _dbContext.RefreshTokens;
            if (_options.IncludeUser)
                query = query.Include(q => q.User);
            return query;
        }

        private IQueryable<RefreshToken> ExtraFilters(
            IQueryable<RefreshToken> query,
            RefreshTokenSearchOptions opts
        )
        {
            if (opts.Ids is { Count: > 0 })
                query = query.Where(x => opts.Ids.Contains(x.Id));
            if (opts.UserIds is { Count: > 0 })
                query = query.Where(x => opts.UserIds.Contains(x.UserId));
            if (opts.DeviceIds is { Count: > 0 })
                query = query.Where(x => x.DeviceId != null && opts.DeviceIds.Contains(x.DeviceId));
            if (opts.RefreshValues is { Count: > 0 })
                query = query.Where(x => x.RefreshValue != null && opts.RefreshValues.Contains(x.RefreshValue));
            return query;
        }
    }
}
