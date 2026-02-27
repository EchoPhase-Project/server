using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Repository;
using EchoPhase.Types.Result.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class WebHookRepository : RepositoryBase<WebHook, WebHookOptions>, IWebHookRepository
    {
        private readonly PostgresContext _dbContext;
        private readonly IIntentsBitMask _intentsService;

        public WebHookRepository(
            PostgresContext dbContext,
            IIntentsBitMask intentsService
        ) : base()
        {
            _dbContext = dbContext;
            _intentsService = intentsService;
        }

        public CursorPage<WebHook> Get(
            WebHookSearchOptions options,
            CursorOptions? cursor = null,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        )
        {
            var query = ApplySearchOptions<WebHook, WebHookSearchOptions>(
                Build(), options, (query, opts) =>
                {
                    query = ExtraFilters(query, opts);
                    if (extraFilters is not null)
                        query = extraFilters(query, opts);
                    return query;
                });

            if (cursor is not null)
                return ApplyCursor(query, cursor, x => x.Id);

            return new CursorPage<WebHook> { Data = query };
        }

        public CursorPage<WebHook> Get(
            Action<WebHookSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        )
        {
            var options = new WebHookSearchOptions();
            configure(options);

            CursorOptions? cursor = null;
            if (configureCursor is not null)
            {
                cursor = new CursorOptions();
                configureCursor(cursor);
            }

            return Get(options, cursor, extraFilters);
        }

        public override IQueryable<WebHook> Build()
        {
            IQueryable<WebHook> query = _dbContext.WebHooks;
            if (_options.IncludeUser)
                query = query.Include(q => q.User);

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
                    .Where(w =>
                    {
                        if (IntentsBitMask.Deserialize(w.Intents).TryGetValue(out var value))
                            return _intentsService.Has(value, opts.Intents.ToArray());
                        return false;
                    })
                    .AsQueryable();

            return query;
        }
    }
}
