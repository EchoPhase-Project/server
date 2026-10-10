// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class ConnectionQuery : RepositoryQuery<Connection>
    {
        public ConnectionQuery(IQueryable<Connection> query)
            : base(query)
        {
        }

        public ConnectionQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public ConnectionQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public ConnectionQuery WithProviders(params string[] providers)
        {
            Where(x => providers.Contains(x.Provider));
            return this;
        }

        public ConnectionQuery WithNames(params string[] names)
        {
            Where(x => names.Contains(x.Name));
            return this;
        }

        public ConnectionQuery WithAuthTypes(params ConnectionAuthType[] types)
        {
            Where(x => types.Contains(x.AuthType));
            return this;
        }

        public ConnectionQuery WithStatuses(params ConnectionStatus[] statuses)
        {
            Where(x => statuses.Contains(x.Status));
            return this;
        }

        public ConnectionQuery WithOAuthAppIds(params Guid[] appIds)
        {
            Where(x => x.OAuthAppId.HasValue && appIds.Contains(x.OAuthAppId.Value));
            return this;
        }

        public ConnectionQuery WithOAuthApp()
        {
            Include(x => x.OAuthApp);
            return this;
        }
    }
}
