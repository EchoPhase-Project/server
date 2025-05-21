namespace EchoPhase.Interfaces
{
	public interface ICacheEntityEntry<T>
	{
		Task SetAsync(T data, TimeSpan cacheDuration);
		Task<T> SetAsync(Func<Task<T>> getData, TimeSpan cacheDuration);
		Task<T> SetAsync(Func<T> getData, TimeSpan cacheDuration);
		Task<T> GetAsync();
		Task<T> GetOrSetAsync(Func<Task<T>> getData, TimeSpan cacheDuration);
		Task<T> GetOrSetAsync(Func<T> getData, TimeSpan cacheDuration);
		Task RemoveAsync();
	}
}
