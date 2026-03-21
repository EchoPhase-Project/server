using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Repository;
using EchoPhase.Types.Result.Extensions;
using Microsoft.EntityFrameworkCore;

// ── WebHookQuery ──────────────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class WebHookQuery : RepositoryQuery<WebHook>
    {
        private readonly IntentsBitMask _intentsBitmask;

        public WebHookQuery(IQueryable<WebHook> query, IntentsBitMask intentsBitmask)
            : base(query)
        {
            _intentsBitmask = intentsBitmask;
        }

        public WebHookQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public WebHookQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public WebHookQuery WithNames(params string[] names)
        {
            Where(x => names.Contains(x.Name));
            return this;
        }

        public WebHookQuery WithUrls(params string[] urls)
        {
            Where(x => urls.Contains(x.Url));
            return this;
        }

        public WebHookQuery WithStatus(WebHookStatus status)
        {
            Where(x => x.Status == status);
            return this;
        }

        public WebHookQuery WithIntents(params string[] intents)
        {
            _query = _query
                .AsEnumerable()
                .Where(w =>
                {
                    if (_intentsBitmask.Deserialize(w.Intents).TryGetValue(out var value))
                        return _intentsBitmask.Has(value, intents);
                    return false;
                })
                .AsQueryable();
            return this;
        }

        public WebHookQuery WithUser()
        {
            Include(x => x.User);
            return this;
        }
    }
}
