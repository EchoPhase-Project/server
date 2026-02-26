namespace EchoPhase.Types.Repository
{
    public interface IRepositoryBase<TO>
    {
        void WithOptions(TO options);
        void WithOptions(Action<TO> configure);
        IQueryable Build();
    }

    public interface IRepositoryBase
    {
        void WithOptions(object options);
        void WithOptions(Action<object> configure);
        IQueryable Build();
    }
}
