using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Security.Cryptography;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class DiscordTokenRepository : RepositoryBase<DiscordToken, DiscordTokenOptions>
    {
        private readonly PostgresContext _dbContext;
        private readonly AesGcm _aes;

        public DiscordTokenRepository(
            PostgresContext dbContext,
            AesGcm aes
        )
        {
            _dbContext = dbContext;
            _aes = aes;
        }

        public CursorPage<DiscordToken> Get(
            DiscordTokenSearchOptions options,
            CursorOptions? cursor = null,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        )
        {
            var query = ApplySearchOptions<DiscordToken, DiscordTokenSearchOptions>(
                Build(), options, (query, opts) =>
                {
                    query = ExtraFilters(query, opts);
                    if (extraFilters is not null)
                        query = extraFilters(query, opts);
                    return query;
                });

            if (cursor is not null)
                return ApplyCursor(query, cursor, x => x.Id);

            return new CursorPage<DiscordToken> { Data = query };
        }

        public CursorPage<DiscordToken> Get(
            Action<DiscordTokenSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        )
        {
            var options = new DiscordTokenSearchOptions();
            configure(options);

            CursorOptions? cursor = null;
            if (configureCursor is not null)
            {
                cursor = new CursorOptions();
                configureCursor(cursor);
            }

            return Get(options, cursor, extraFilters);
        }

        public override IQueryable<DiscordToken> Build()
        {
            IQueryable<DiscordToken> query = _dbContext.DiscordTokens;

            if (_options.IncludeUser)
                query = query.Include(q => q.User);

            return query;
        }

        private IQueryable<DiscordToken> ExtraFilters(
            IQueryable<DiscordToken> query,
            DiscordTokenSearchOptions opts
        )
        {
            if (opts.Ids is { Count: > 0 })
                query = query
                    .Where(x => opts.Ids.Contains(x.Id));

            if (opts.UserIds is { Count: > 0 })
                query = query
                    .Where(x => opts.UserIds.Contains(x.UserId));

            if (opts.Names is { Count: > 0 })
                query = query
                    .Where(x => opts.Names.Contains(x.Name));

            if (opts.Tokens is { Count: > 0 })
                query = query
                    .AsEnumerable()
                    .Where(x => opts.Tokens.Contains(_aes.DecryptToBase64(x.Token)))
                    .AsQueryable();

            return query;
        }
    }
}
