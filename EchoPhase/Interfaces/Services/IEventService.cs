namespace EchoPhase.Interfaces
{
	public interface IEventService
	{
		public Task SendMessageToAllAsync<T, TS>(T message, ISet<string> intents, TS? shardId)
			where TS : struct;
		public Task SendMessageToUsersAsync<T, TS>(ISet<Guid> userIds, T message, ISet<string> intents, TS? shardId)
			where TS : struct;
		public Task SendMessageToRolesAsync<T, TS>(ISet<string> roles, T message, ISet<string> intents, TS? shardId)
			where TS : struct;
	}
}
