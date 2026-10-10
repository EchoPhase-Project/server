// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class ApiClientQuery : RepositoryQuery<ApiClient>
    {
        public ApiClientQuery(IQueryable<ApiClient> query) : base(query) { }

        public ApiClientQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public ApiClientQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public ApiClientQuery WithPermissions()
        {
            Include(x => x.Permissions);
            return this;
        }
    }
}
