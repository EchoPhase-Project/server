using StackExchange.Redis;

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
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns>
        /// A task representing the asynchronous operation. The result is <c>true</c> if the key exists; otherwise, <c>false</c>.
        /// </returns>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Asynchronously retrieves the value of the prefixed key from Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns>
        /// A task representing the asynchronous operation. The result is a <see cref="IServiceResult{T}"/> containing the byte array, or an error if the key is not found.
        /// </returns>
        Task<IServiceResult<byte[]>> GetAsync(string key);

        /// <summary>
        /// Asynchronously sets the value of the prefixed key in Redis with optional expiration and flags.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="value">The byte array value to store.</param>
        /// <param name="expiry">Optional expiration time for the key.</param>
        /// <param name="keepTtl">If <c>true</c>, preserves the existing TTL (if any).</param>
        /// <param name="when">Condition for setting the value (e.g., only if not exists).</param>
        /// <param name="flags">Additional Redis command flags.</param>
        /// <returns>
        /// A task representing the asynchronous operation. The result is <c>true</c> if the value was set successfully; otherwise, <c>false</c>.
        /// </returns>
        Task<bool> SetAsync(
            string key,
            byte[] value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        /// <summary>
        /// Asynchronously retrieves the value for the given key. If the key does not exist, generates the value using the provided asynchronous generator, stores it, and returns it.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="generator">An optional asynchronous function that generates the value if the key is missing.</param>
        /// <returns>
        /// A task representing the asynchronous operation. The result is a <see cref="IServiceResult{T}"/> with the existing or newly generated value.
        /// </returns>
        Task<IServiceResult<byte[]>> GetOrSetAsync(string key, Func<Task<byte[]>>? generator = null);

        /// <summary>
        /// Asynchronously retrieves the value for the given key. If the key does not exist, generates the value using the provided synchronous generator, stores it, and returns it.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="syncGenerator">A synchronous function that generates the value if the key is missing.</param>
        /// <returns>
        /// A task representing the asynchronous operation. The result is a <see cref="IServiceResult{T}"/> with the existing or newly generated value.
        /// </returns>
        Task<IServiceResult<byte[]>> GetOrSetAsync(string key, Func<byte[]> syncGenerator);

        // Sync methods

        /// <summary>
        /// Synchronously checks whether a key exists in Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns><c>true</c> if the key exists; otherwise, <c>false</c>.</returns>
        bool Exists(string key);

        /// <summary>
        /// Synchronously retrieves the value of the prefixed key from Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns>
        /// A <see cref="IServiceResult{T}"/> containing the byte array, or an error if the key does not exist.
        /// </returns>
        IServiceResult<byte[]> Get(string key);

        /// <summary>
        /// Synchronously sets the value of the prefixed key in Redis with optional expiration and flags.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="value">The byte array value to store.</param>
        /// <param name="expiry">Optional expiration time for the key.</param>
        /// <param name="keepTtl">If <c>true</c>, preserves the existing TTL (if any).</param>
        /// <param name="when">Condition for setting the value (e.g., only if not exists).</param>
        /// <param name="flags">Additional Redis command flags.</param>
        /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
        bool Set(
            string key,
            byte[] value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        /// <summary>
        /// Synchronously retrieves the value for the given key. If the key does not exist, generates the value using the provided generator, stores it, and returns it.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="generator">An optional function that generates the value if the key is missing. If <c>null</c>, a default generator is used.</param>
        /// <returns>
        /// A <see cref="IServiceResult{T}"/> containing the existing or newly generated value, or an error if storing failed.
        /// </returns>
        IServiceResult<byte[]> GetOrSet(string key, Func<byte[]>? generator = null);
    }
}
