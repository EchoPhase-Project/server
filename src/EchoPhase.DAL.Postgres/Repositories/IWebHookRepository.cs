using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public interface IWebHookRepository : IRepositoryBase<WebHookOptions>
    {
        public IEnumerable<WebHook> Get(
            WebHookSearchOptions options,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        );
        public IEnumerable<WebHook> Get(
            Action<WebHookSearchOptions> configure,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        );
    }
}
