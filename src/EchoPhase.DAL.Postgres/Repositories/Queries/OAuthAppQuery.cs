// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class OAuthAppQuery : RepositoryQuery<OAuthApp>
    {
        public OAuthAppQuery(IQueryable<OAuthApp> query)
            : base(query)
        {
        }

        public OAuthAppQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public OAuthAppQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public OAuthAppQuery WithProviders(params string[] providers)
        {
            Where(x => providers.Contains(x.Provider));
            return this;
        }

        public OAuthAppQuery WithClientIds(params string[] clientIds)
        {
            Where(x => clientIds.Contains(x.ClientId));
            return this;
        }
    }
}
