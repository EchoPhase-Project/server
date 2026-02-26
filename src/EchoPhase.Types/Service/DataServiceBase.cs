using EchoPhase.Types.Repository;

namespace EchoPhase.Types.Service
{
    public abstract class DataServiceBase<TR, TO> : IDataServiceBase<TR>
        where TR : IRepositoryBase<TO>
    {
        protected readonly TR _repository;

        public DataServiceBase(
            TR repository
        )
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
