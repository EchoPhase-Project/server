// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class UserPermissionGrantQuery : RepositoryQuery<UserPermissionGrant>
    {
        public UserPermissionGrantQuery(IQueryable<UserPermissionGrant> query) : base(query) { }

        public UserPermissionGrantQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public UserPermissionGrantQuery WithResources(params string[] resources)
        {
            Where(x => resources.Contains(x.Resource));
            return this;
        }

        public UserPermissionGrantQuery Active(DateTime now)
        {
            Where(x => x.ExpiresAt == null || x.ExpiresAt > now);
            return this;
        }

        /// <summary>Expired grants whose expiry has not yet revoked tokens.</summary>
        public UserPermissionGrantQuery ExpiryDue(DateTime now)
        {
            Where(x => x.ExpiresAt != null && x.ExpiresAt <= now && !x.ExpiryApplied);
            return this;
        }
    }
}
