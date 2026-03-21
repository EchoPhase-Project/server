// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;

// ── RepositoryBase<TEntity> ───────────────────────────────────────────────────

namespace EchoPhase.Types.Repository
{
    public abstract class RepositoryBase<TEntity> : IRepositoryBase<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        public abstract IQueryable<TEntity> Build();
        public abstract void Add(TEntity entity);
        public abstract void Update(TEntity entity);
        public abstract void Remove(TEntity entity);
        public abstract Task<int> SaveAsync(CancellationToken ct = default);

        public virtual RepositoryQuery<TEntity> Query() => new(Build());

        public virtual async Task<int> Set(
            TEntity entity,
            Action<TEntity>? configure = null,
            CancellationToken ct = default)
        {
            configure?.Invoke(entity);
            entity.UpdatedAt = DateTime.UtcNow;
            var exists = Build().Any(e => e.Id == entity.Id);
            if (exists) Update(entity);
            else Add(entity);
            return await SaveAsync(ct);
        }
    }
}
