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

        public CursorPage<RefreshToken> Get(
            RefreshTokenSearchOptions options,
            CursorOptions? cursor = null,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null
        )
        {
            var query = ApplySearchOptions<RefreshToken, RefreshTokenSearchOptions>(
                Build(), options, (query, opts) =>
                {
                    query = ExtraFilters(query, opts);
                    if (extraFilters is not null)
                        query = extraFilters(query, opts);
                    return query;
                });

            if (cursor is not null)
                return ApplyCursor(query, cursor, x => x.Id);

            return new CursorPage<RefreshToken> { Data = query };
        }

        public CursorPage<RefreshToken> Get(
            Action<RefreshTokenSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null
        )
        {
            var options = new RefreshTokenSearchOptions();
            configure(options);

            CursorOptions? cursor = null;
            if (configureCursor is not null)
            {
                cursor = new CursorOptions();
                configureCursor(cursor);
            }

            return Get(options, cursor, extraFilters);
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
