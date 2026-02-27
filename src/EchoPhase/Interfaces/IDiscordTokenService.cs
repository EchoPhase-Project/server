using System.Linq.Expressions;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Result;
using EchoPhase.Types.Service;
using EchoPhase.Types.Repository;

namespace EchoPhase.Interfaces
{
    public interface IDiscordTokenService : IDataServiceBase<DiscordToken, DiscordTokenRepository, DiscordTokenOptions>
    {
        CursorPage<DiscordToken> Get(
            DiscordTokenSearchOptions opts,
            CursorOptions? cursor = null,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        );
        CursorPage<DiscordToken> Get(
            Action<DiscordTokenSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
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
