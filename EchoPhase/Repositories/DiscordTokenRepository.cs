using Microsoft.EntityFrameworkCore;

using EchoPhase.Models;
using EchoPhase.Services.Security;
using EchoPhase.DAL.Postgres;
using EchoPhase.Repositories.Options;

namespace EchoPhase.Repositories
{
    public class DiscordTokenRepository : RepositoryBase<DiscordTokenRepository, DiscordTokenOptions>
	{
		private readonly PostgresContext _dbContext;
		private readonly AesService _aesService;

		public DiscordTokenRepository(
			PostgresContext dbContext,
			AesService aesService
		)
		{
			_dbContext = dbContext;
			_aesService = aesService;
		}

		public IEnumerable<DiscordToken> Get(
			DiscordTokenSearchOptions options,
			Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
		)
		{
			var query = ApplySearchOptions<DiscordToken, DiscordTokenSearchOptions>(
				Build(), options, (query, opts) =>
				{
					query = ExtraFilters(query, opts);

					if (extraFilters != null)
						query = extraFilters(query, opts);

					return query;
				});

			return query;
		}

		public IEnumerable<DiscordToken> Get(
			Action<DiscordTokenSearchOptions> configure,
			Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
		)
		{
			var options = new DiscordTokenSearchOptions();
			configure(options);
			return Get(options, extraFilters);
		}

		public override IQueryable<DiscordToken> Build()
		{
			IQueryable<DiscordToken> query = _dbContext.DiscordTokens;

			if (_options.IncludeUser)
				query.Include(q => q.User);

			return query;
		}

		private IQueryable<DiscordToken> ExtraFilters(
			IQueryable<DiscordToken> query,
			DiscordTokenSearchOptions opts
		)
		{
			if (opts.Ids is { Count: > 0 })
				query = query
					.Where(x => opts.Ids.Contains(x.Id));

			if (opts.UserIds is { Count: > 0 })
				query = query
					.Where(x => opts.UserIds.Contains(x.UserId));

			if (opts.Names is { Count: > 0 })
				query = query
					.Where(x => opts.Names.Contains(x.Name));

			if (opts.Tokens is { Count: > 0 })
				query = query
					.AsEnumerable()
					.Where(x => opts.Tokens.Contains(_aesService.Decrypt(x.Token)))
					.AsQueryable();

			return query;
		}
	}
}
