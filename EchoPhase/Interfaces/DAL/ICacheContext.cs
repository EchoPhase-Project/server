using EchoPhase.DAL.Redis;

namespace EchoPhase.Interfaces
{
	public interface ICacheContext
	{
		RedisEntityEntry<T> Entry<T>(string key);
	}
}
