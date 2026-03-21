// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EchoPhase.Identity;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.WebSockets.Exceptions;
using Microsoft.Extensions.Logging;

namespace EchoPhase.WebSockets
{
    public class WebSocketService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly IRoleService _roleService;
        private readonly IntentsBitMask _intentsBitmask;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly ILogger<WebSocketService> _logger;

        public WebSocketService(
            IRoleService roleService,
            IntentsBitMask intentsBitmask,
            WebSocketConnectionManager connectionManager,
            ILogger<WebSocketService> logger)
        {
            _roleService = roleService;
            _intentsBitmask = intentsBitmask;
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public async Task SendMessageAsync<T>(WebSocket webSocket, T message)
        {
            if (webSocket.State != WebSocketState.Open)
            {
                _logger.LogWarning("Cannot send message: WebSocket is not open (State: {State})",
                    webSocket.State);
                return;
            }

            try
            {
                var messageBytes = Encoding.UTF8.GetBytes(SerializeMessage(message));
                await webSocket.SendAsync(
                    new ArraySegment<byte>(messageBytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error while sending message");
                throw;
            }
        }

        public Task SendMessageAsync<T>(WebSocketConnection connection, T message) =>
            SendMessageAsync(connection.WebSocket, message);

        public async Task SendMessageAsync<T>(
            WebSocketConnection connection,
            T message,
            ISet<string> requiredIntents)
        {
            if (!_intentsBitmask.Has(connection.Intents, requiredIntents.ToArray()))
            {
                _logger.LogWarning(
                    "Connection {ConnectionId} lacks required intents. Required: {Required}, Has: {Has}",
                    connection.Id,
                    string.Join(", ", requiredIntents),
                    connection.Intents);

                throw new MissingIntentsException(
                    "Connection {0} lacks required intents. Required: {1}",
                    connection.Id,
                    string.Join(", ", requiredIntents));
            }

            await SendMessageAsync(connection, message);
        }

        public async Task SendMessageToUserAsync<T>(
            Guid userId,
            T message,
            ISet<string> requiredIntents)
        {
            if (!_connectionManager.TryGetConnections(userId, out var connections) ||
                connections is null ||
                connections.Count == 0)
            {
                _logger.LogDebug("No active connections for UserId: {UserId}", userId);
                return;
            }

            var tasks = connections
                .Select(async connection =>
                {
                    try
                    {
                        await SendMessageAsync(connection, message, requiredIntents);
                    }
                    catch (MissingIntentsException)
                    {
                        // Connection doesn't have required intents, skip
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error sending message to connection {ConnectionId}",
                            connection.Id);
                    }
                });

            await Task.WhenAll(tasks);
        }

        public async Task SendMessageToUserAsync<T, TShardId>(
            Guid userId,
            T message,
            ISet<string> requiredIntents,
            TShardId shardId)
            where TShardId : struct
        {
            if (!_connectionManager.TryGetConnections(userId, out var connections) ||
                connections is null ||
                connections.Count == 0)
            {
                _logger.LogDebug("No active connections for UserId: {UserId}", userId);
                return;
            }

            var shardIndex = CalculateShardIndex(shardId, connections.Count);
            var connection = connections[shardIndex];

            try
            {
                await SendMessageAsync(connection, message, requiredIntents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending sharded message to connection {ConnectionId} (shard {Shard}/{Total})",
                    connection.Id, shardIndex, connections.Count);
            }
        }

        public async Task SendMessageToUsersAsync<T>(
            ISet<Guid> userIds,
            T message,
            ISet<string> requiredIntents)
        {
            var tasks = userIds.Select(userId =>
                SendMessageToUserAsync(userId, message, requiredIntents));

            await Task.WhenAll(tasks);
        }

        public async Task SendMessageToUsersAsync<T, TShardId>(
            ISet<Guid> userIds,
            T message,
            ISet<string> requiredIntents,
            TShardId shardId)
            where TShardId : struct
        {
            var tasks = userIds.Select(userId =>
                SendMessageToUserAsync(userId, message, requiredIntents, shardId));

            await Task.WhenAll(tasks);
        }

        public async Task BroadcastMessageAsync<T>(T message, ISet<string> requiredIntents)
        {
            var userIds = _connectionManager.GetConnectedUserIds().ToHashSet();
            await SendMessageToUsersAsync(userIds, message, requiredIntents);
        }

        public async Task BroadcastMessageAsync<T, TShardId>(
            T message,
            ISet<string> requiredIntents,
            TShardId shardId)
            where TShardId : struct
        {
            var userIds = _connectionManager.GetConnectedUserIds().ToHashSet();
            await SendMessageToUsersAsync(userIds, message, requiredIntents, shardId);
        }

        public async Task SendMessageToRoleAsync<T>(
            string role,
            T message,
            ISet<string> requiredIntents)
        {
            var usersWithRole = await _roleService.GetUsersInRoleAsync(role);
            var userIds = usersWithRole.Select(u => u.Id).ToHashSet();
            await SendMessageToUsersAsync(userIds, message, requiredIntents);
        }

        public async Task SendMessageToRoleAsync<T, TShardId>(
            string role,
            T message,
            ISet<string> requiredIntents,
            TShardId shardId)
            where TShardId : struct
        {
            var usersWithRole = await _roleService.GetUsersInRoleAsync(role);
            var userIds = usersWithRole.Select(u => u.Id).ToHashSet();
            await SendMessageToUsersAsync(userIds, message, requiredIntents, shardId);
        }

        public async Task SendMessageToRolesAsync<T>(
            ISet<string> roles,
            T message,
            ISet<string> requiredIntents)
        {
            var usersWithRole = await _roleService.GetUsersInRolesAsync(roles);
            var userIds = usersWithRole.Select(u => u.Id).ToHashSet();
            await SendMessageToUsersAsync(userIds, message, requiredIntents);
        }

        public async Task SendMessageToRolesAsync<T, TShardId>(
            ISet<string> roles,
            T message,
            ISet<string> requiredIntents,
            TShardId shardId)
            where TShardId : struct
        {
            var usersWithRole = await _roleService.GetUsersInRolesAsync(roles);
            var userIds = usersWithRole.Select(u => u.Id).ToHashSet();
            await SendMessageToUsersAsync(userIds, message, requiredIntents, shardId);
        }

        private int CalculateShardIndex<T>(T value, int shardCount) where T : struct
        {
            if (shardCount <= 0)
                throw new ArgumentException("Shard count must be positive", nameof(shardCount));

            ReadOnlySpan<T> span = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(span);

            ulong hash = BitConverter.ToUInt64(bytes.ComputeXxHash3());
            return (int)(hash % (ulong)shardCount);
        }

        private string SerializeMessage<T>(T message)
        {
            if (message is string str)
                return str;

            return JsonSerializer.Serialize(message, JsonOptions);
        }
    }
}
