// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.Types.Repository;

namespace EchoPhase.Types.Service
{
    /// <summary>
    /// Full read/write service contract.
    /// Mirrors the CQRS split introduced in <see cref="IRepositoryBase{TEntity}"/>.
    /// </summary>
    public interface IDataServiceBase<TEntity, TR>
        : IReadServiceBase<TEntity, TR>
        where TR : IRepositoryBase<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    { }
}
