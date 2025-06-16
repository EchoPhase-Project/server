namespace EchoPhase.Interfaces
{
    /// <summary>
    /// Interface for managing Redis keys with a fixed prefix.
    /// Provides both synchronous and asynchronous operations.
    /// </summary>
    public interface IKeysService
    {
        // Async methods

        /// <summary>
        /// Asynchronously checks whether a key exists in Redis.
        /// </summary>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Asynchronously gets the value of a Redis key.
        /// </summary>
        Task<string> GetAsync(string key);

        /// <summary>
        /// Asynchronously sets the value of a Redis key.
        /// </summary>
        Task<bool> SetAsync(string key, string value);

        /// <summary>
        /// Asynchronously gets the value of a Redis key, or sets it using an async generator if it does not exist.
        /// </summary>
        Task<string> GetOrSetAsync(string key, Func<Task<string>>? generator = null);

        /// <summary>
        /// Asynchronously gets the value of a Redis key, or sets it using a sync generator if it does not exist.
        /// </summary>
        Task<string> GetOrSetAsync(string key, Func<string> syncGenerator);

        // Sync methods

        /// <summary>
        /// Synchronously checks whether a key exists in Redis.
        /// </summary>
        bool Exists(string key);

        /// <summary>
        /// Synchronously gets the value of a Redis key.
        /// </summary>
        string Get(string key);

        /// <summary>
        /// Synchronously sets the value of a Redis key.
        /// </summary>
        bool Set(string key, string value);

        /// <summary>
        /// Synchronously gets the value of a Redis key, or sets it using a generator if it does not exist.
        /// </summary>
        string GetOrSet(string key, Func<string>? generator = null);
    }
}
