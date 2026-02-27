using EchoPhase.Types.Repository;

namespace EchoPhase.Types.Service
{
    public interface IDataServiceBase<TEntity, TR, TO>
        where TR : IRepositoryBase<TEntity, TO>
        where TEntity : class
        where TO : class, new()
    {
        void ConfigureRepository(Action<TR> action);
        Task ConfigureRepositoryAsync(Func<TR, Task> action);
    }
}
