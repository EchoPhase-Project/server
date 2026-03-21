// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Interfaces.Services
{
    public interface IEventService
    {
        Task SendMessageToAllAsync<T, TS>(T message, HashSet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToUsersAsync<T, TS>(HashSet<Guid> userIds, T message, HashSet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToRolesAsync<T, TS>(HashSet<string> roles, T message, HashSet<string> intents, TS shardId)
            where TS : struct;
        Task SendMessageToAllAsync<T>(T message, HashSet<string> intents);

        Task SendMessageToUsersAsync<T>(HashSet<Guid> userIds, T message, HashSet<string> intents);

        Task SendMessageToRolesAsync<T>(HashSet<string> roles, T message, HashSet<string> intents);
    }
}
