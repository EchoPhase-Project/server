using System.Net.WebSockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EchoPhase.Exceptions;
using EchoPhase.Interfaces;
using EchoPhase.Services.WebSockets.Models;

namespace EchoPhase.Services.WebSockets
{
    public class WebSocketService
    {
        private readonly IRoleService _roleService;
        private readonly IIntentsService _intentsService;
        private readonly WebSocketConnectionManager _connectionManager;

        public WebSocketService(
            IRoleService roleService,
            IIntentsService intentsService,
            WebSocketConnectionManager connectionManager
        )
        {
            _roleService = roleService;
            _intentsService = intentsService;
            _connectionManager = connectionManager;
        }

        public async Task SendMessageToConnectionAsync<T>(WebSocket webSocket, T message)
        {
            if (webSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("Unacceptable WebSocket state. Cannot send message.");

            var messageBytes = Encoding.UTF8.GetBytes(SerializeMessage(message));
            var messageSegment = new ArraySegment<byte>(messageBytes);
            await webSocket.SendAsync(messageSegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task SendMessageToConnectionAsync<T>(WebSocketConnection connection, T message) =>
            await SendMessageToConnectionAsync(connection.WebSocket, message);

        public async Task SendMessageToConnectionAsync<T>(WebSocketConnection connection, T message, ISet<string> intents)
        {
            if (!_intentsService.Has(connection.Intents, intents))
                throw new MissingIntentsException(
                    "{hash}: Connection lacks required intents. Expected: {actual}",
                    connection.WebSocket.GetHashCode().ToString(),
                    string.Join(", ", intents)
                );

            await SendMessageToConnectionAsync(connection, message);
        }

        public async Task SendMessageToUserAsync<T, TS>(Guid userId, T message, ISet<string> intents, TS? shardId = null)
            where TS : struct
        {
            var connections = _connectionManager.GetConnections(userId);
            if (connections.Count == 0)
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

        public async Task SendMessageToUsersAsync<T, TS>(ISet<Guid> userIds, T message, ISet<string> intents, TS? shardId = null)
            where TS : struct
        {
            foreach (var userId in userIds)
            {
                await SendMessageToUserAsync(userId, message, intents, shardId);
            }
        }

        public async Task SendMessageToAllAsync<T, TS>(T message, ISet<string> intents, TS? shardId = null)
            where TS : struct
        {
            var userIds = _connectionManager.GetConnections().Select(c => c.Key).ToHashSet();
            await SendMessageToUsersAsync(userIds, message, intents, shardId);
        }

        public async Task SendMessageToRoleAsync<T, TS>(string role, T message, ISet<string> intents, TS? shardId = null)
            where TS : struct
        {
            var usersWithRole = await _roleService.GetUsersInRoleAsync(role);
            var userIds = usersWithRole.Select(u => u.Id).ToHashSet();
            await SendMessageToUsersAsync(userIds, message, intents, shardId);
        }

        public async Task SendMessageToRolesAsync<T, TS>(ISet<string> roles, T message, ISet<string> intents, TS? shardId = null)
            where TS : struct
        {
            var usersWithRole = await _roleService.GetUsersInRolesAsync(roles);
            var userIds = usersWithRole.Select(u => u.Id).ToHashSet();
            await SendMessageToUsersAsync(userIds, message, intents, shardId);
        }

        private int CalculateShardIndex<T>(T id, int count) where T : struct
        {
            BigInteger BIid = new BigInteger(ToByteArray<T>(id), isUnsigned: true, isBigEndian: false);
            return (int)(BIid % count);
        }

        private byte[] ToByteArray<T>(T value) where T : struct
        {
            ReadOnlySpan<T> span = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(span);
            return bytes.ToArray();
        }

        private string SerializeMessage<T>(T message) =>
            message is string str ? str : JsonSerializer.Serialize(message);
    }
}
