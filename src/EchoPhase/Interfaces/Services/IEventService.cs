namespace EchoPhase.Interfaces
{
    public interface IEventService
    {
        Task SendMessageToAllAsync<T, TS>(T message, ISet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToUsersAsync<T, TS>(ISet<Guid> userIds, T message, ISet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToRolesAsync<T, TS>(ISet<string> roles, T message, ISet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToAllAsync<T>(T message, ISet<string> intents);

        Task SendMessageToUsersAsync<T>(ISet<Guid> userIds, T message, ISet<string> intents);

        Task SendMessageToRolesAsync<T>(ISet<string> roles, T message, ISet<string> intents);
    }
}
