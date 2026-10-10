// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class SessionQuery : RepositoryQuery<Session>
    {
        public SessionQuery(IQueryable<Session> query) : base(query) { }

        public SessionQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public SessionQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public SessionQuery WithDeviceIds(params string[] deviceIds)
        {
            Where(x => x.DeviceId != null && deviceIds.Contains(x.DeviceId));
            return this;
        }

        public SessionQuery WithRefreshTokenHashes(params string[] hashes)
        {
            Where(x => hashes.Contains(x.RefreshTokenHash));
            return this;
        }

        public SessionQuery WithUser()
        {
            Include(x => x.User);
            return this;
        }

        public SessionQuery WithAudits()
        {
            Include(x => x.Audits);
            return this;
        }
    }
}
