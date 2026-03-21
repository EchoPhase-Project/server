// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

// ── RefreshTokenQuery ─────────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class RefreshTokenQuery : RepositoryQuery<RefreshToken>
    {
        public RefreshTokenQuery(IQueryable<RefreshToken> query) : base(query) { }

        public RefreshTokenQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public RefreshTokenQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public RefreshTokenQuery WithDeviceIds(params string[] deviceIds)
        {
            Where(x => x.DeviceId != null && deviceIds.Contains(x.DeviceId));
            return this;
        }

        public RefreshTokenQuery WithRefreshValues(params string[] values)
        {
            Where(x => values.Contains(x.RefreshValue));
            return this;
        }

        public RefreshTokenQuery WithUser()
        {
            Include(x => x.User);
            return this;
        }

        public RefreshTokenQuery WithAudits()
        {
            Include(x => x.Audits);
            return this;
        }

        public RefreshTokenQuery WithClaims()
        {
            Include(x => x.Scopes);
            Include(x => x.Intents);
            Include(x => x.Permissions);
            return this;
        }
    }
}
