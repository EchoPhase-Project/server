// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Authentication;
using EchoPhase.Security.Cryptography.Vaults;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Authentication.Secrets
{
    /// <summary>
    /// Hashes session refresh tokens under <c>Authentication:Refresh:Key</c>.
    /// </summary>
    public sealed class SessionTokenHasher : SecretHasher
    {
        public SessionTokenHasher(IOptions<AuthenticationOptions> settings, KeyRing keys)
            : base(keys, settings.Value.Refresh.Key)
        {
        }
    }
}
