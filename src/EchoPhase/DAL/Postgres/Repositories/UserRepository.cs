using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class UserRepository : RepositoryBase<UserOptions>
    {
        private readonly PostgresContext _dbContext;

        public UserRepository(PostgresContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IEnumerable<User> Get(
            UserSearchOptions options,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
        )
        {
            var query = ApplySearchOptions<User, UserSearchOptions>(
                Build(), options, (query, opts) =>
                {
                    query = ExtraFilters(query, opts);

                    if (extraFilters != null)
                        query = extraFilters(query, opts);

                    return query;
                });

            return query;
        }

        public IEnumerable<User> Get(
            Action<UserSearchOptions> configure,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
        )
        {
            var options = new UserSearchOptions();
            configure(options);
            return Get(options, extraFilters);
        }

        public override IQueryable<User> Build()
        {
            IQueryable<User> query = _dbContext.Users;
            if (_options.IncludeWebHooks)
                query.Include(q => q.WebHooks);

            if (_options.IncludeRefreshTokens)
                query.Include(q => q.RefreshTokens);

            if (_options.IncludeDiscordTokens)
                query.Include(q => q.DiscordTokens);

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
