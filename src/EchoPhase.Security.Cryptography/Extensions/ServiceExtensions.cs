// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Cryptography.Vaults;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Cryptography.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddCryptography(this IServiceCollection services)
        {
            services.AddSingleton<AesGcm>();
            services.AddSingleton<ICrypto25519, Crypto25519>();
            services.AddTransient<IKeyVault, KeyVault>();
            services.AddTransient<ISecretVault, SecretVault>();

            return services;
        }
    }
}
