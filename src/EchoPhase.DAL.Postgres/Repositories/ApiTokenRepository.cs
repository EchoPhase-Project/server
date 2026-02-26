using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Security.Cryptography;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class DiscordTokenRepository : RepositoryBase<DiscordTokenOptions>
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

        public IEnumerable<DiscordToken> Get(
            DiscordTokenSearchOptions options,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        )
        {
            var query = ApplySearchOptions<DiscordToken, DiscordTokenSearchOptions>(
                Build(), options, (query, opts) =>
                {
                    query = ExtraFilters(query, opts);

                    if (extraFilters != null)
                        query = extraFilters(query, opts);

                    return query;
                });

            return query;
        }

        public IEnumerable<DiscordToken> Get(
            Action<DiscordTokenSearchOptions> configure,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        )
        {
            var options = new DiscordTokenSearchOptions();
            configure(options);
            return Get(options, extraFilters);
        }

        public override IQueryable<DiscordToken> Build()
        {
            IQueryable<DiscordToken> query = _dbContext.DiscordTokens;

            if (_options.IncludeUser)
                query.Include(q => q.User);

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
