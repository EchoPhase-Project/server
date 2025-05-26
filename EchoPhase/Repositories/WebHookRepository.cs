using Microsoft.EntityFrameworkCore;

using EchoPhase.Models;
using EchoPhase.Interfaces;
using EchoPhase.DAL.Postgres;
using EchoPhase.Repositories.Options;

namespace EchoPhase.Repositories
{
    public class WebHookRepository : RepositoryBase<WebHookRepository, WebHookOptions>, IWebHookRepository
	{
		private readonly PostgresContext _dbContext;
		private readonly IIntentsService _intentsService;

		public WebHookRepository(
			PostgresContext dbContext,
			IIntentsService intentsService
		) : base()
		{
			_dbContext = dbContext;
			_intentsService = intentsService;
		}

		public IEnumerable<WebHook> Get(
			WebHookSearchOptions options,
			Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
		)
		{
			var query = ApplySearchOptions<WebHook, WebHookSearchOptions>(
				Build(), options, (query, opts) =>
					{
						query = ExtraFilters(query, opts);

						if (extraFilters != null)
							query = extraFilters(query, opts);
						
						return query;
					});

			return query;
		}

		public IEnumerable<WebHook> Get(
			Action<WebHookSearchOptions> configure,
			Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
		)
		{
			var options = new WebHookSearchOptions();
			configure(options);
			return Get(options, extraFilters);
		}

		public override IQueryable<WebHook> Build()
		{
			IQueryable<WebHook> query = _dbContext.WebHooks;
			if (_options.IncludeUser)
				query.Include(q => q.User);

			return query;
		}

		private IQueryable<WebHook> ExtraFilters(
			IQueryable<WebHook> query,
			WebHookSearchOptions opts
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

			if (opts.Urls is { Count: > 0 })
				query = query
					.Where(x => opts.Urls.Contains(x.Url));

			if (opts.Intents is { Count: > 0 })
				query = query
					.AsEnumerable()
					.Where(w => _intentsService.Has(w.Intents, opts.Intents))
					.AsQueryable();

			return query;
		}
	}
}
