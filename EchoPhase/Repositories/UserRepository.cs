using Microsoft.EntityFrameworkCore;

using EchoPhase.Models;
using EchoPhase.DAL.Postgres;
using EchoPhase.Repositories.Options;

namespace EchoPhase.Repositories
{
    public class UserRepository : RepositoryBase<UserRepository, UserOptions>
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

			if (_options.IncludeJwtTokens)
				query.Include(q => q.JwtTokens);

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
					.Where(x => opts.UserNames.Contains(x.UserName ?? ""));

			if (opts.Emails is { Count: > 0 })
				query = query
					.Where(x => opts.Emails.Contains(x.Email ?? ""));

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
