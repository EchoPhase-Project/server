
namespace EchoPhase.Interfaces
{
    public interface IDataServiceBase<TR>
    {
        void ConfigureRepository(Action<TR> action);
        Task ConfigureRepositoryAsync(Func<TR, Task> action);
    }
}
