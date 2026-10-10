// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;

namespace EchoPhase.DAL.Abstractions.Service
{
    /// <summary>
    /// Read-only service contract — depends only on <see cref="IReadRepository{TEntity}"/>.
    /// Use when a service never mutates data.
    /// </summary>
    public interface IReadServiceBase<TEntity, TR>
        where TR : IReadRepository<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        void ConfigureRepository(Action<TR> action);
        Task ConfigureRepositoryAsync(Func<TR, Task> action);
    }
}

