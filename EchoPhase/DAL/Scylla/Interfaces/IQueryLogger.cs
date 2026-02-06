namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface IQueryLogger
    {
        void LogQuery(string cql, Type resultType, object[] parameters);
        void LogQueryTiming(long elapsedMs);
        void LogError(string cql, Exception ex);
    }
}
