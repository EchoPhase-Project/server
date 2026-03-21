// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Redis.Database;
namespace EchoPhase.DAL.Redis.Interfaces
{
    public interface ICacheContext
    {
        CacheEntry<T> Entry<T>(string key);
    }
}
