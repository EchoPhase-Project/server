// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;

namespace EchoPhase.DAL.Abstractions.Service
{
    /// <summary>
    /// Base implementation for services that own a repository.
    /// Implements the full <see cref="IDataServiceBase{TEntity,TR}"/>.
    /// </summary>
    public abstract class DataServiceBase<TEntity, TR> : IDataServiceBase<TEntity, TR>
        where TR : IRepositoryBase<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        protected readonly TR Repository;

        protected DataServiceBase(TR repository)
        {
            Repository = repository;
        }

        public virtual void ConfigureRepository(Action<TR> action)
            => action(Repository);

        public virtual Task ConfigureRepositoryAsync(Func<TR, Task> action)
            => action(Repository);
    }
}
