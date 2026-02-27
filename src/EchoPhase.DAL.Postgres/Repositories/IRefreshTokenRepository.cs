using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public interface IRefreshTokenRepository : IRepositoryBase<RefreshToken, RefreshTokenOptions>
    {
        CursorPage<RefreshToken> Get(
            RefreshTokenSearchOptions options,
            CursorOptions? cursor = null,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null
        );
        CursorPage<RefreshToken> Get(
            Action<RefreshTokenSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
            Func<IQueryable<RefreshToken>, RefreshTokenSearchOptions, IQueryable<RefreshToken>>? extraFilters = null
        );
    }
}
