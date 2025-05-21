using Microsoft.Extensions.Caching.Distributed;

namespace EchoPhase.DAL.Redis
{
	public class RedisSet<T>
	{
		private readonly string _template;
		private readonly IDistributedCache _cache;

		public RedisSet(
				IDistributedCache cache,
				string template)
		{
			_cache = cache;
			_template = template;
		}

		private string FormatKey(string key) =>
			_template.Replace("{key}", key);

		public RedisEntityEntry<T> this[string key] => 
			new RedisEntityEntry<T>(_cache, FormatKey(key));
	}
}
