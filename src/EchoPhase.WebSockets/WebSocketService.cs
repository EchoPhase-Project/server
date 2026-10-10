// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EchoPhase.Identity;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Extensions;
using Microsoft.Extensions.Logging;

namespace EchoPhase.WebSockets
{
    /// <summary>
    /// Sends to gateway connections. A message is serialized once and the
    /// same bytes go to every recipient; a connection without the required
    /// intents is skipped; a failed send is logged and does not stop the rest.
    /// </summary>
    public class WebSocketService
    {
        // Sent to clients, never embedded in HTML: keep quotes and non-ASCII readable.
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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

        /// <summary>To one connection, whatever its intents (protocol replies).</summary>
        public Task SendAsync<T>(WebSocketConnection connection, T message, CancellationToken ct = default)
            => SendBytesAsync(connection, Serialize(message), ct);

        public Task SendMessageToUsersAsync<T>(
            ISet<Guid> userIds, T message, ISet<string> requiredIntents, CancellationToken ct = default)
        {
            var bytes = Serialize(message);
            var required = Required(requiredIntents);
            return Task.WhenAll(userIds
                .SelectMany(id => Eligible(id, required))
                .Select(c => SendBytesAsync(c, bytes, ct)));
        }

        /// <summary>To one connection per user, picked by hashing <paramref name="shardId"/>.</summary>
        public Task SendMessageToUsersAsync<T, TShardId>(
            ISet<Guid> userIds, T message, ISet<string> requiredIntents, TShardId shardId, CancellationToken ct = default)
            where TShardId : struct
        {
            var bytes = Serialize(message);
            var required = Required(requiredIntents);
            return Task.WhenAll(userIds
                .Select(id => Eligible(id, required))
                .Where(eligible => eligible.Count > 0)
                .Select(eligible => SendBytesAsync(eligible[ShardIndex(shardId, eligible.Count)], bytes, ct)));
        }

        public Task BroadcastMessageAsync<T>(T message, ISet<string> requiredIntents, CancellationToken ct = default)
            => SendMessageToUsersAsync(_connectionManager.GetConnectedUserIds().ToHashSet(), message, requiredIntents, ct);

        public Task BroadcastMessageAsync<T, TShardId>(
            T message, ISet<string> requiredIntents, TShardId shardId, CancellationToken ct = default)
            where TShardId : struct
            => SendMessageToUsersAsync(_connectionManager.GetConnectedUserIds().ToHashSet(), message, requiredIntents, shardId, ct);

        public async Task SendMessageToRoleAsync<T>(
            string role, T message, ISet<string> requiredIntents, CancellationToken ct = default)
            => await SendMessageToUsersAsync(await UsersInAsync(new HashSet<string> { role }), message, requiredIntents, ct);

        public async Task SendMessageToRolesAsync<T>(
            ISet<string> roles, T message, ISet<string> requiredIntents, CancellationToken ct = default)
            => await SendMessageToUsersAsync(await UsersInAsync(roles), message, requiredIntents, ct);

        public async Task SendMessageToRolesAsync<T, TShardId>(
            ISet<string> roles, T message, ISet<string> requiredIntents, TShardId shardId, CancellationToken ct = default)
            where TShardId : struct
            => await SendMessageToUsersAsync(await UsersInAsync(roles), message, requiredIntents, shardId, ct);

        public static byte[] Serialize<T>(T message)
            => message is string text ? Encoding.UTF8.GetBytes(text) : JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        private async Task<HashSet<Guid>> UsersInAsync(ISet<string> roles)
            => (await _roleService.GetUsersInRolesAsync(roles)).Select(u => u.Id).ToHashSet();

        private string[] Required(ISet<string> intents) => [.. intents];

        // Only users with sockets here cost anything: others are not in the manager.
        private List<WebSocketConnection> Eligible(Guid userId, string[] required)
        {
            var connections = _connectionManager.GetConnections(userId);
            var eligible = new List<WebSocketConnection>(connections.Count);
            foreach (var connection in connections)
                if (connection.IsOpen && _intentsBitmask.Has(connection.Intents, required))
                    eligible.Add(connection);
            return eligible;
        }

        private async Task SendBytesAsync(WebSocketConnection connection, byte[] bytes, CancellationToken ct)
        {
            try
            {
                await connection.SendAsync(bytes, ct);
            }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or OperationCanceledException)
            {
                _logger.LogDebug(ex, "Could not send to connection {ConnectionId}", connection.Id);
            }
        }

        private static int ShardIndex<T>(T value, int shardCount) where T : struct
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
            ulong hash = BitConverter.ToUInt64(bytes.ComputeXxHash3());
            return (int)(hash % (ulong)shardCount);
        }
    }
}
