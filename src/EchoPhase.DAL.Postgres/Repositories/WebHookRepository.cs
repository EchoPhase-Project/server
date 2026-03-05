using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Repository;
using EchoPhase.Types.Result.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class WebHookRepository
        : RepositoryBase<WebHook, WebHookOptions, WebHookSearchOptions>
    {
        private readonly PostgresContext _dbContext;
        private readonly IntentsBitMask  _intentsService;

        public WebHookRepository(PostgresContext dbContext, IntentsBitMask intentsService)
            : base()
        {
            _dbContext       = dbContext;
            _intentsService  = intentsService;
        }

        public override IQueryable<WebHook> Build()
        {
            IQueryable<WebHook> query = _dbContext.WebHooks;
            if (_options.IncludeUser)
                query = query.Include(q => q.User);
            return query;
        }

        protected override IQueryable<WebHook> ApplyExtraFilters(
            IQueryable<WebHook> query,
            WebHookSearchOptions opts)
        {
            if (opts.Ids      is { Count: > 0 }) query = query.Where(x => opts.Ids.Contains(x.Id));
            if (opts.UserIds  is { Count: > 0 }) query = query.Where(x => opts.UserIds.Contains(x.UserId));
            if (opts.Names    is { Count: > 0 }) query = query.Where(x => opts.Names.Contains(x.Name));
            if (opts.Urls     is { Count: > 0 }) query = query.Where(x => opts.Urls.Contains(x.Url));

            if (opts.Intents is { Count: > 0 })
                query = query
                    .AsEnumerable()
                    .Where(w =>
                    {
                        if (_intentsService.Deserialize(w.Intents).TryGetValue(out var value))
                            return _intentsService.Has(value, opts.Intents.ToArray());
                        return false;
                    })
                    .AsQueryable();

            return query;
        }
    }
}
