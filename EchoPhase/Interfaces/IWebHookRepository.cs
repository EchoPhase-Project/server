using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;

namespace EchoPhase.Interfaces
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
