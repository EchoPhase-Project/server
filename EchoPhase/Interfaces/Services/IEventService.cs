using EchoPhase.Processors.Enums;

namespace EchoPhase.Interfaces
{
	public interface IEventService
	{
		public Task SendMessageToAllAsync<T>(T message, IntentsFlags intents, Guid? shardId);
		public Task SendMessageToUserAsync<T>(Guid userId, T message, IntentsFlags intents, Guid? shardId);
		public Task SendMessageToRoleAsync<T>(string role, T message, IntentsFlags intents, Guid? shardId);
	}
}
