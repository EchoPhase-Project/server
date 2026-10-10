// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class ImpersonationQuery : RepositoryQuery<Impersonation>
    {
        public ImpersonationQuery(IQueryable<Impersonation> query) : base(query) { }

        public ImpersonationQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public ImpersonationQuery WithActorIds(params Guid[] actorIds)
        {
            Where(x => actorIds.Contains(x.ActorId));
            return this;
        }

        public ImpersonationQuery WithTargetIds(params Guid[] targetIds)
        {
            Where(x => targetIds.Contains(x.TargetId));
            return this;
        }

        /// <summary>Records where the user took part, as actor or as target.</summary>
        public ImpersonationQuery Involving(Guid userId)
        {
            Where(x => x.ActorId == userId || x.TargetId == userId);
            return this;
        }

        public ImpersonationQuery Active(DateTime now)
        {
            Where(x => x.EndedAt == null && x.ExpiresAt > now);
            return this;
        }
    }
}
