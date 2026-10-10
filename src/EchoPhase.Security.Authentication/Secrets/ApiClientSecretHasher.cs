// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Authentication;
using EchoPhase.Security.Cryptography.Vaults;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Authentication.Secrets
{
    /// <summary>
    /// Hashes API client secrets under <c>Authentication:ApiClients:Key</c>.
    /// </summary>
    public sealed class ApiClientSecretHasher : SecretHasher
    {
        public ApiClientSecretHasher(IOptions<AuthenticationOptions> settings, KeyRing keys)
            : base(keys, settings.Value.ApiClients.Key)
        {
        }
    }
}
