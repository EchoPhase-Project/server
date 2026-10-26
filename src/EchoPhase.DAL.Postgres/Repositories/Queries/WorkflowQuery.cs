// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class WorkflowQuery : RepositoryQuery<Workflow>
    {
        public WorkflowQuery(IQueryable<Workflow> query)
            : base(query)
        {
        }

        public WorkflowQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public WorkflowQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public WorkflowQuery WithPackageIds(params Guid[] packageIds)
        {
            Where(x => packageIds.Contains(x.PackageId));
            return this;
        }

        public WorkflowQuery WithKeys(params Guid[] keys)
        {
            Where(x => keys.Contains(x.Key));
            return this;
        }
    }
}
