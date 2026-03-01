using EchoPhase.Types.Result;
using StackExchange.Redis;

namespace EchoPhase.Clients.Discord
{
    public interface IDiscordSecretVault
    {
        // --------------------------
        // Exists
        // --------------------------

        Task<bool> ExistsAsync(string userId, string keyName);
        bool Exists(string userId, string keyName);

        // --------------------------
        // Get
        // --------------------------

        Task<IServiceResult<T>> GetAsync<T>(string userId, string keyName);
        IServiceResult<T> Get<T>(string userId, string keyName);

        // --------------------------
        // Set
        // --------------------------

        Task<bool> SetAsync<T>(
            string userId,
            string keyName,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        bool Set<T>(
            string userId,
            string keyName,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None);

        // --------------------------
        // GetOrSet
        // --------------------------

        Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<Task<T>>? generator = null);

        Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<T> generator);

        // --------------------------
        // Delete
        // --------------------------

        Task<bool> DeleteAsync(string userId, string keyName);
        bool Delete(string userId, string keyName);
        Task DeleteAllAsync(string userId);

        // --------------------------
        // Index
        // --------------------------

        Task<IEnumerable<string>> GetUserKeyNamesAsync(string userId);
        Task<IDictionary<string, IServiceResult<T>>> GetAllAsync<T>(string userId);
    }
}
