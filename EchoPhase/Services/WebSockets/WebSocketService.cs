using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Numerics;

using EchoPhase.Processors.Enums;
using EchoPhase.Services.WebSockets.Models;
using EchoPhase.Exceptions;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;

namespace EchoPhase.Services.WebSockets
{
	public class WebSocketService
	{
		private readonly WebSocketConnectionManager _connectionManager;
		private readonly IUserService _userService;
		private readonly IAuthService _authService;

		public WebSocketService(
			WebSocketConnectionManager connectionManager, 
			IUserService userService,
			IAuthService authService
		)
		{
			_connectionManager = connectionManager;
			_userService = userService;
			_authService = authService;
		}

		public async Task SendMessageToConnectionAsync<T>(WebSocket webSocket, T message)
		{
			if (webSocket != null && webSocket.State == WebSocketState.Open)
			{
				var messageBytes = Encoding.UTF8.GetBytes(SerializeMessage(message));
				var messageSegment = new ArraySegment<byte>(messageBytes);
				await webSocket.SendAsync(messageSegment, WebSocketMessageType.Text, true, CancellationToken.None);
			}
		}

		public async Task SendMessageToConnectionAsync<T>(WebSocketConnection connection, T message) =>
			await SendMessageToConnectionAsync(connection.WebSocket, message);

		public async Task SendMessageToConnectionAsync<T>(WebSocketConnection connection, T message, IntentsFlags intents)
		{
			if (intents.HasIntents(connection.Intents))
				await SendMessageToConnectionAsync(connection, message);
			else
				throw new MissingIntentsException(
					"{hash}: Connection lacks required intents. Expected: {required}, got: {actual}",
					connection.WebSocket.GetHashCode().ToString(),
					(long)intents,
					(long)connection.Intents
				);
		}

		public async Task SendMessageToUserAsync<T>(Guid userId, T message, IntentsFlags intents, Guid? shardId = null)
		{
			List<WebSocketConnection>? connections = _connectionManager.GetConnections(userId);
			if (connections is null || connections.Count == 0)
				return;

			if (shardId is Guid id)
			{
				int index = CalculateShardIndex(id, connections.Count);
				var connection = connections[index];
				await SendMessageToConnectionAsync(connection, message, intents);
				return;
			}

			foreach (var connection in connections)
			{
				await SendMessageToConnectionAsync(connection, message, intents);
			}
		}

		public async Task SendMessageToAllAsync<T>(T message, IntentsFlags intents, Guid? shardId = null)
		{
			foreach (var connection in _connectionManager.GetConnections())
			{
				await SendMessageToUserAsync(connection.Key, message, intents, shardId);
			}
		}

		public async Task SendMessageToRoleAsync<T>(string role, T message, IntentsFlags intents, Guid? shardId = null)
		{
			var allUserIds = _connectionManager.GetUserIds();

			var userIds = (await Task.WhenAll(
				allUserIds.Select(async u => new 
				{
					UserId = u,
					IsInRole = await _authService.IsInRoleAsync(u, role)
				})))
				.Where(x => x.IsInRole)
				.Select(x => x.UserId)
				.ToList();

			foreach (var userId in userIds)
			{
				await SendMessageToUserAsync(userId, message, intents, shardId);
			}
		}

		private int CalculateShardIndex(Guid id, int count)
		{
			BigInteger BIid = new BigInteger(id.ToByteArray(), isUnsigned: true, isBigEndian: false);
			return (int)(BIid % count);
		}

		private string SerializeMessage<T>(T message) =>
			message is string str ? str : JsonSerializer.Serialize(message);
	}
}
