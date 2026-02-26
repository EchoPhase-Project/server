namespace EchoPhase.Types.Service
{
    public interface IDataServiceBase<TR>
    {
        void ConfigureRepository(Action<TR> action);
        Task ConfigureRepositoryAsync(Func<TR, Task> action);
    }
}
