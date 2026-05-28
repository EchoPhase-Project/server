// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>
    /// Returned by <see cref="DiscordWebSocketManager.Subscribe"/>.
    /// Disposing this object automatically removes the subscriber from the manager.
    /// </summary>
    public sealed class DiscordWebSocketSubscription : IDisposable
    {
        private readonly DiscordWebSocketManager _manager;
        private readonly Guid _userId;
        private readonly IDiscordWebSocketSubscriber _subscriber;
        private bool _disposed;

        internal DiscordWebSocketSubscription(
            DiscordWebSocketManager manager,
            Guid userId,
            IDiscordWebSocketSubscriber subscriber)
        {
            _manager = manager;
            _userId = userId;
            _subscriber = subscriber;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _manager.Unsubscribe(_userId, _subscriber);
        }
    }
}
