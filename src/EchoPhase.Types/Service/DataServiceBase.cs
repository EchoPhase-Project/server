using EchoPhase.Types.Repository;

namespace EchoPhase.Types.Service
{
    public abstract class DataServiceBase<TEntity, TR, TO> : IDataServiceBase<TEntity, TR, TO>
        where TR : IRepositoryBase<TEntity, TO>
        where TEntity : class
        where TO : class, new()
    {
        protected readonly TR _repository;

        public DataServiceBase(TR repository)
        {
            _repository = repository;
        }

        public virtual void ConfigureRepository(Action<TR> action)
        {
            action(_repository);
        }

        public virtual Task ConfigureRepositoryAsync(Func<TR, Task> action)
        {
            return action(_repository);
        }
    }
}
