// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;

namespace EchoPhase.Types.Repository
{
    /// <summary>
    /// Read-only access to a repository.
    /// </summary>
    public interface IReadRepository<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        IQueryable<TEntity> Build();
        RepositoryQuery<TEntity> Query();
    }
}
