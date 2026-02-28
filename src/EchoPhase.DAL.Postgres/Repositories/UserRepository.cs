using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class UserRepository : RepositoryBase<User, UserOptions>
    {
        private readonly PostgresContext _dbContext;

        public UserRepository(PostgresContext dbContext)
        {
            _dbContext = dbContext;
        }

        public CursorPage<User> Get(
            UserSearchOptions options,
            CursorOptions? cursor = null,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
        )
        {
            var query = ApplySearchOptions<User, UserSearchOptions>(
                Build(), options, (query, opts) =>
                {
                    query = ExtraFilters(query, opts);
                    if (extraFilters is not null)
                        query = extraFilters(query, opts);
                    return query;
                });

            if (cursor is not null)
                return ApplyCursor(query, cursor, x => x.Id);

            return new CursorPage<User> { Data = query };
        }

        public CursorPage<User> Get(
            Action<UserSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
        )
        {
            var options = new UserSearchOptions();
            configure(options);

            CursorOptions? cursor = null;
            if (configureCursor is not null)
            {
                cursor = new CursorOptions();
                configureCursor(cursor);
            }

            return Get(options, cursor, extraFilters);
        }

        public override IQueryable<User> Build()
        {
            IQueryable<User> query = _dbContext.Users;
            if (_options.IncludeWebHooks)
                query = query.Include(q => q.WebHooks);

            if (_options.IncludeRefreshTokens)
                query = query.Include(q => q.RefreshTokens);

            return query;
        }

        private IQueryable<User> ExtraFilters(
            IQueryable<User> query,
            UserSearchOptions opts
        )
        {
            if (opts.Ids is { Count: > 0 })
                query = query
                    .Where(x => opts.Ids.Contains(x.Id));

            if (opts.Names is { Count: > 0 })
                query = query
                    .Where(x => opts.Names.Contains(x.Name));

            if (opts.UserNames is { Count: > 0 })
                query = query
                    .Where(x => x.UserName != null && opts.UserNames.Contains(x.UserName));

            if (opts.Emails is { Count: > 0 })
                query = query
                    .Where(x => x.Email != null && opts.Emails.Contains(x.Email));

            /*
			if (_options.IncludeWebHooks && opts.WebHookUrls is { Count: > 0 })
				query = query
					.Where(x =>
						x.WebHooks.Any(c =>
							opts.WebHookUrls.Contains(c.Url)));
					.AsEnumerable()
					.Select<User, User>(x => {
						x.WebHooks = x.WebHooks.Where(c =>
							opts.WebHookUrls.Contains(c.Url)).ToList();
						return x;
					}).AsQueryable();
			*/

            return query;
        }
    }
}
