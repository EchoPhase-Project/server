// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;

// ── WebHookQuery ──────────────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class WebHookQuery : RepositoryQuery<WebHook>
    {
        public WebHookQuery(IQueryable<WebHook> query)
            : base(query)
        {
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

        /// <summary>Webhooks subscribed to every one of <paramref name="intents"/>.</summary>
        public WebHookQuery WithIntents(params string[] intents)
        {
            // Npgsql: @intents <@ "Intents" (commutator of @>), served by the GIN index.
            Where(x => intents.All(i => x.Intents.Contains(i)));
            return this;
        }

        public WebHookQuery WithUser()
        {
            Include(x => x.User);
            return this;
        }
    }
}
