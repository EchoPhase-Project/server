using System.Reflection;
using EchoPhase.Clients.Attributes;
using EchoPhase.Clients.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Clients.Extensions
{
    public static class ClientTokenProviderExtensions
    {
        public static IServiceCollection AddClientTokenProviders(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var targetAssemblies = assemblies.Length > 0
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies();

            var types = targetAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    typeof(IClientTokenProvider).IsAssignableFrom(t) &&
                    t is { IsClass: true, IsAbstract: false } &&
                    t.GetCustomAttribute<ProviderNameAttribute>() is not null);

            foreach (var type in types)
            {
                var name = type.GetCustomAttribute<ProviderNameAttribute>()!.Name;

                services.AddKeyedScoped(
                    typeof(IClientTokenProvider),
                    name,
                    (sp, _) => (IClientTokenProvider)ActivatorUtilities.CreateInstance(sp, type));
            }

            services.AddScoped<IClientTokenProviderRegistry, ClientTokenProviderRegistry>();

            return services;
        }
    }
}
