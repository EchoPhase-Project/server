using System.Collections.Concurrent;
using EchoPhase.Security.Authentication.Jwt.Claims.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Authentication.Jwt.Claims
{
    public class ClaimsProviderRegistry : IClaimsProviderRegistry
    {
        private static readonly ConcurrentDictionary<Type, ObjectFactory> _factories = new();
        private static readonly IReadOnlyList<Type> _providerTypes;
        private readonly IHttpContextAccessor _httpContextAccessor;

        static ClaimsProviderRegistry()
        {
            _providerTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    t is { IsAbstract: false, IsInterface: false } &&
                    t.IsAssignableTo(typeof(IClaimsProvider)))
                .ToList();
        }

        public ClaimsProviderRegistry(IHttpContextAccessor httpContextAccessor)
            => _httpContextAccessor = httpContextAccessor;

        public IReadOnlyList<IClaimsProvider> GetProviders()
        {
            var serviceProvider = _httpContextAccessor.HttpContext?.RequestServices
                ?? throw new InvalidOperationException("No active HTTP context.");

            return _providerTypes
                .Select(type =>
                {
                    var factory = _factories.GetOrAdd(
                        type,
                        t => ActivatorUtilities.CreateFactory(t, []));
                    return (IClaimsProvider)factory(serviceProvider, null);
                })
                .ToList();
        }
    }
}
