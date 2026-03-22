// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Cryptography.Vaults.Strategies
{
    /// <summary>
    /// Determines how a logical key name is translated into the final Redis storage key.
    /// </summary>
    public interface IKeyStrategy
    {
        /// <summary>
        /// Builds the final Redis key from a logical key name.
        /// </summary>
        /// <param name="key">Logical key (without any prefix or tenant information).</param>
        /// <returns>The Redis-ready key string.</returns>
        string Build(string key);
    }
}
