using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

// ── UserQuery ─────────────────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class UserQuery : RepositoryQuery<User>
    {
        public UserQuery(IQueryable<User> query) : base(query) { }

        public UserQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public UserQuery WithNames(params string[] names)
        {
            Where(x => names.Contains(x.Name));
            return this;
        }

        public UserQuery WithUserNames(params string[] userNames)
        {
            Where(x => x.UserName != null && userNames.Contains(x.UserName));
            return this;
        }

        public UserQuery WithEmails(params string[] emails)
        {
            Where(x => x.Email != null && emails.Contains(x.Email));
            return this;
        }

        public UserQuery WithRefreshTokens()
        {
            Include(x => x.RefreshTokens);
            return this;
        }

        public UserQuery WithWebHooks()
        {
            Include(x => x.WebHooks);
            return this;
        }
    }
}
