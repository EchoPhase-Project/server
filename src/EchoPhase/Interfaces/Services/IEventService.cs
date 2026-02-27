namespace EchoPhase.Interfaces
{
    public interface IEventService
    {
        Task SendMessageToAllAsync<T, TS>(T message, HashSet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToUsersAsync<T, TS>(HashSet<Guid> userIds, T message, HashSet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToRolesAsync<T, TS>(HashSet<string> roles, T message, HashSet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToAllAsync<T>(T message, HashSet<string> intents);

        Task SendMessageToUsersAsync<T>(HashSet<Guid> userIds, T message, HashSet<string> intents);

        Task SendMessageToRolesAsync<T>(HashSet<string> roles, T message, HashSet<string> intents);
    }
}
