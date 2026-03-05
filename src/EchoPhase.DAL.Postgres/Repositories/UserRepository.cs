using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class UserRepository
        : RepositoryBase<User, UserOptions, UserSearchOptions>
    {
        private readonly PostgresContext _dbContext;

        public UserRepository(PostgresContext dbContext) : base()
        {
            _dbContext = dbContext;
        }

        public override IQueryable<User> Build()
        {
            IQueryable<User> query = _dbContext.Users;
            if (_options.IncludeWebHooks)
                query = query.Include(q => q.WebHooks);
            if (_options.IncludeRefreshTokens)
                query = query.Include(q => q.RefreshTokens);
            return query;
        }

        protected override IQueryable<User> ApplyExtraFilters(
            IQueryable<User> query,
            UserSearchOptions opts)
        {
            if (opts.Ids       is { Count: > 0 }) query = query.Where(x => opts.Ids.Contains(x.Id));
            if (opts.Names     is { Count: > 0 }) query = query.Where(x => opts.Names.Contains(x.Name));
            if (opts.UserNames is { Count: > 0 }) query = query.Where(x => x.UserName != null && opts.UserNames.Contains(x.UserName));
            if (opts.Emails    is { Count: > 0 }) query = query.Where(x => x.Email != null && opts.Emails.Contains(x.Email));
            return query;
        }
    }
}
