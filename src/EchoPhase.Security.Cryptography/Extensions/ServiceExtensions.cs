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
