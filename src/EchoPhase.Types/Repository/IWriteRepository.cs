// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;

namespace EchoPhase.Types.Repository
{
    /// <summary>
    /// Write-only access to a repository.
    /// </summary>
    public interface IWriteRepository<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        void Add(TEntity entity);
        void Update(TEntity entity);
        void Remove(TEntity entity);
        Task<int> SaveAsync(CancellationToken ct = default);
        Task<int> Set(TEntity entity, Action<TEntity>? configure = null, CancellationToken ct = default);
    }
}
