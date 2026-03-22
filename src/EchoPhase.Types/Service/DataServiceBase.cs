// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.Types.Repository;

namespace EchoPhase.Types.Service
{
    /// <summary>
    /// Base implementation for services that own a repository.
    /// Implements the full <see cref="IDataServiceBase{TEntity,TR}"/>.
    /// </summary>
    public abstract class DataServiceBase<TEntity, TR> : IDataServiceBase<TEntity, TR>
        where TR : IRepositoryBase<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        protected readonly TR _repository;

        protected DataServiceBase(TR repository)
        {
            _repository = repository;
        }

        public virtual void ConfigureRepository(Action<TR> action)
            => action(_repository);

        public virtual Task ConfigureRepositoryAsync(Func<TR, Task> action)
            => action(_repository);
    }
}
