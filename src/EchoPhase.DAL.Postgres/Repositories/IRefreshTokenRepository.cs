using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public interface IRefreshTokenRepository : IRepositoryBase<RefreshToken, RefreshTokenOptions>
    {
        IEnumerable<RefreshToken> Get(RefreshTokenSearchOptions options,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null);

        IEnumerable<RefreshToken> Get(Action<RefreshTokenSearchOptions> configure,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null);
    }
}
