using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface ISaveStrategy
    {
        Task<int> ExecuteAsync(IEnumerable<TrackedEntity> entities, Transaction? transaction);
        int Execute(IEnumerable<TrackedEntity> entities, Transaction? transaction);
    }
}
