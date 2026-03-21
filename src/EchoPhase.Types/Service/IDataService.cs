using EchoPhase.DAL.Abstractions;
using EchoPhase.Types.Repository;

namespace EchoPhase.Types.Service
{
    public interface IDataServiceBase<TEntity, TR>
        where TR : IRepositoryBase<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        void ConfigureRepository(Action<TR> action);
        Task ConfigureRepositoryAsync(Func<TR, Task> action);
    }
}
