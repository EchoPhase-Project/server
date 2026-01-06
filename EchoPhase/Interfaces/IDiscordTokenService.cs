using System.Linq.Expressions;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Postgres.Repositories.Options;

namespace EchoPhase.Interfaces
{
    public interface IDiscordTokenService : IDataServiceBase<DiscordTokenRepository>
    {
        IEnumerable<DiscordToken> Get(
            DiscordTokenSearchOptions opts,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        );
        IEnumerable<DiscordToken> Get(
            Action<DiscordTokenSearchOptions> configure,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        );
        Task<IServiceResult> CreateAsync(DiscordToken token);
        Task<IServiceResult<DiscordToken>> CreateAsync(Action<DiscordToken> configure);
        Task<IServiceResult<DiscordToken>> EditAsync(
            DiscordToken token,
            DiscordToken modifyData,
            params Expression<Func<DiscordToken, object>>[] overrideFields
        );
        Task<IServiceResult<DiscordToken>> EditAsync(
             DiscordToken token,
             Action<DiscordToken> configure,
             params Expression<Func<DiscordToken, object>>[] overrideFields
         );
        Task<IServiceResult> DeleteAsync(DiscordToken token);
    }
}
