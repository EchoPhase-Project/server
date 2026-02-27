using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public interface IWebHookRepository : IRepositoryBase<WebHook, WebHookOptions>
    {
        CursorPage<WebHook> Get(
            WebHookSearchOptions options,
            CursorOptions? cursor = null,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        );
        CursorPage<WebHook> Get(
            Action<WebHookSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        );
    }
}
