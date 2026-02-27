namespace EchoPhase.Types.Repository
{
    public interface IRepositoryBase<TEntity, TO>
    {
        void WithOptions(TO options);
        void WithOptions(Action<TO> configure);
        IQueryable<TEntity> Build();
    }

    public interface IRepositoryBase
    {
        void WithOptions(object options);
        void WithOptions(Action<object> configure);
        IQueryable<T> Build<T>();
    }
}
