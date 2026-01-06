namespace EchoPhase.DAL.Redis.Interfaces
{
    public interface ICacheContext
    {
        CacheEntry<T> Entry<T>(string key);
    }
}
