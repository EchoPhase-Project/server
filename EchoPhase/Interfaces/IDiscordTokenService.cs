using System.Linq.Expressions;
using EchoPhase.Models;
using EchoPhase.Repositories.Options;
using EchoPhase.Services;

namespace EchoPhase.Interfaces
{
    public interface IDiscordTokenService : IDataService<DiscordTokenService, DiscordTokenOptions>
    {
        public IEnumerable<DiscordToken> Get(
            DiscordTokenSearchOptions opts,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        );
        public IEnumerable<DiscordToken> Get(
            Action<DiscordTokenSearchOptions> configure,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        );
        public Task<DiscordToken> CreateAsync(DiscordToken token);
        public Task<IDiscordTokenResult> CreateAsync(params IEnumerable<DiscordToken> tokens);
        public Task<DiscordToken> EditAsync(
            DiscordToken token,
            DiscordToken modifyData,
            params Expression<Func<DiscordToken, object>>[] overrideFields
        );
        public Task<IDiscordTokenResult> EditAsync(
            IEnumerable<DiscordToken> tokens,
            DiscordToken modifyData,
            params Expression<Func<DiscordToken, object>>[] overrideFields
        );
        public Task<DiscordToken> DeleteAsync(DiscordToken token);
        public Task<IDiscordTokenResult> DeleteAsync(params IEnumerable<DiscordToken> tokens);
    }
}
