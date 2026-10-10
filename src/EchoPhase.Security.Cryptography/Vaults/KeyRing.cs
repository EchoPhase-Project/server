// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using EchoPhase.Types.Result.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Cryptography.Vaults
{
    /// <summary>
    /// Process-wide cache of named keys from <see cref="IKeyVault"/>. A key is
    /// read (or created) on first use, not when its consumer is constructed,
    /// and then served from memory: services hold a <see cref="KeyRing"/> and
    /// ask for the key when they need it.
    /// </summary>
    /// <remarks>
    /// A failed read is not cached (<see cref="LazyThreadSafetyMode.PublicationOnly"/>),
    /// so a Redis outage at startup heals once Redis is back.
    /// </remarks>
    public sealed class KeyRing
    {
        private readonly IServiceProvider _services;
        private readonly ConcurrentDictionary<string, Lazy<byte[]>> _keys = new(StringComparer.Ordinal);

        public KeyRing(IServiceProvider services)
        {
            _services = services;
        }

        public byte[] Get(string name)
            => _keys.GetOrAdd(name, n => new Lazy<byte[]>(() => Load(n), LazyThreadSafetyMode.PublicationOnly)).Value;

        private byte[] Load(string name)
        {
            var result = _services.GetRequiredService<IKeyVault>().GetOrSet(name);

            result.OnFailure(err => throw new InvalidOperationException(err.Value));

            return result.TryGetValue(out var key)
                ? key
                : throw new InvalidOperationException($"Missing '{name}' key.");
        }
    }
}
