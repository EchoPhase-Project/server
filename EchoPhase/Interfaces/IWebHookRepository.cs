using EchoPhase.Models;
using EchoPhase.Repositories;
using EchoPhase.Repositories.Options;

namespace EchoPhase.Interfaces
{
    public interface IWebHookRepository : IRepositoryBase<WebHookRepository, WebHookOptions>
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
