// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EchoPhase.Identity;
using EchoPhase.WebSockets.Cluster;
using Microsoft.Extensions.Logging;

namespace EchoPhase.WebSockets
{
    /// <summary>
    /// Sends to gateway connections. A message is serialized once and the
    /// same bytes go to every recipient (through <see cref="GatewayDispatcher"/>,
    /// on every replica); a connection without the required intents is
    /// skipped; a failed send is logged and does not stop the rest.
    /// </summary>
    public class WebSocketService
    {
        // Sent to clients, never embedded in HTML: keep quotes and non-ASCII readable.
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly IRoleService _roleService;
        private readonly GatewayDispatcher _dispatcher;
        private readonly ILogger<WebSocketService> _logger;

        public WebSocketService(
            IRoleService roleService,
            GatewayDispatcher dispatcher,
            ILogger<WebSocketService> logger)
        {
            _roleService = roleService;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        /// <summary>To one connection, whatever its intents (protocol replies).</summary>
        public Task SendAsync<T>(WebSocketConnection connection, T message, CancellationToken ct = default)
            => SendBytesAsync(connection, Serialize(message), ct);

        /// <summary>
        /// To the connections of <paramref name="userIds"/>, on every replica:
        /// all shared ones, and one member of each shard pool.
        /// </summary>
        /// <param name="shardKey">Picks the member in each pool (the same key, the same member while it stays); null: any.</param>
        public Task SendMessageToUsersAsync<T>(
            ISet<Guid> userIds, T message, ISet<string> requiredIntents, CancellationToken ct = default, string? shardKey = null)
            => _dispatcher.SendAsync([.. userIds], Serialize(message), [.. requiredIntents], shardKey, ct);

        /// <summary>The same, to every user.</summary>
        public Task BroadcastMessageAsync<T>(
            T message, ISet<string> requiredIntents, CancellationToken ct = default, string? shardKey = null)
            => _dispatcher.SendAsync(null, Serialize(message), [.. requiredIntents], shardKey, ct);

        public async Task SendMessageToRoleAsync<T>(
            string role, T message, ISet<string> requiredIntents, CancellationToken ct = default, string? shardKey = null)
            => await SendMessageToUsersAsync(await UsersInAsync(new HashSet<string> { role }), message, requiredIntents, ct, shardKey);

        public async Task SendMessageToRolesAsync<T>(
            ISet<string> roles, T message, ISet<string> requiredIntents, CancellationToken ct = default, string? shardKey = null)
            => await SendMessageToUsersAsync(await UsersInAsync(roles), message, requiredIntents, ct, shardKey);

        public static byte[] Serialize<T>(T message)
            => message is string text ? Encoding.UTF8.GetBytes(text) : JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        private async Task<HashSet<Guid>> UsersInAsync(ISet<string> roles)
            => (await _roleService.GetUsersInRolesAsync(roles)).Select(u => u.Id).ToHashSet();

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
    }
}
