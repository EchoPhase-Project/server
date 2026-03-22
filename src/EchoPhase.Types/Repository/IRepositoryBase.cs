// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;

namespace EchoPhase.Types.Repository
{
    /// <summary>
    /// Full read/write repository contract.
    /// </summary>
    public interface IRepositoryBase<TEntity>
        : IReadRepository<TEntity>, IWriteRepository<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    { }
}
