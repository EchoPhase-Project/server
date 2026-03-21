// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Interfaces.Services
{
    public interface IUserConnectionManager
    {
        void KeepUserConnection(Guid userId, string connectionId);
        void RemoveUserConnection(Guid userId, string connectionId);
        string? GetConnectionId(Guid userId);
        bool IsOnline(Guid userId);
        IEnumerable<Guid> GetAllOnlineUsers();
    }
}
