using System.Collections.Concurrent;

using EchoPhase.Interfaces;

namespace EchoPhase.Hubs.Managers
{
    public class UserConnectionManager : IUserConnectionManager
    {
        private static readonly ConcurrentDictionary<Guid, string> userConnections = new();

        public void KeepUserConnection(Guid userId, string connectionId)
        {
            userConnections[userId] = connectionId;
        }

        public void RemoveUserConnection(Guid userId, string connectionId)
        {
            userConnections.TryRemove(userId, out _);
        }

        public string? GetConnectionId(Guid userId)
        {
            userConnections.TryGetValue(userId, out var connectionId);
            return connectionId;
        }

        public bool IsOnline(Guid userId)
        {
            return userConnections.ContainsKey(userId);
        }

        public IEnumerable<Guid> GetAllOnlineUsers()
        {
            return userConnections.Keys.AsEnumerable();
        }
    }
}
