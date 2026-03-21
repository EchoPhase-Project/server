using EchoPhase.DAL.Abstractions;
using EchoPhase.Types.Repository;

namespace EchoPhase.Types.Service
{
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
