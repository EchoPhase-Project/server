using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Reflection;
using EchoPhase.Security.Authorization.Builders;
using EchoPhase.Security.Authorization.Attributes;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Authorization
{
    public class DynamicPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;
        private readonly IServiceProvider _services;

        private readonly ConcurrentDictionary<string, Lazy<IPolicyBuilder>> _builders;

        public DynamicPolicyProvider(
            IOptions<AuthorizationOptions> options,
            IServiceProvider services,
            IEnumerable<Assembly> assemblies)
        {
            _fallback = new DefaultAuthorizationPolicyProvider(options);
            _services = services;
            _builders = new ConcurrentDictionary<string, Lazy<IPolicyBuilder>>(
                StringComparer.Ordinal);

            RegisterFromAssemblies(assemblies);
        }

        private void RegisterFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes()
                    .Where(t =>
                        t is { IsClass: true, IsAbstract: false } &&
                        typeof(IPolicyBuilder).IsAssignableFrom(t) &&
                        t.IsDefined(typeof(PolicyPrefixAttribute), inherit: false));

                foreach (var type in types)
                {
                    var prefix = type
                        .GetCustomAttribute<PolicyPrefixAttribute>()!
                        .Prefix;

                    if (!_builders.TryAdd(prefix, new Lazy<IPolicyBuilder>(
                        () => (IPolicyBuilder)ActivatorUtilities.CreateInstance(_services, type),
                        LazyThreadSafetyMode.ExecutionAndPublication)))
                    {
                        var existing = _builders[prefix].Value.GetType().Name;
                        throw new InvalidOperationException(
                            $"Policy prefix '{prefix}' is already registered by '{existing}'. " +
                            $"Cannot register '{type.Name}' with the same prefix.");
                    }
                }
            }
        }

        public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            foreach (var (prefix, lazy) in _builders)
            {
                if (!policyName.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var body = policyName[prefix.Length..];
                return lazy.Value.Build(body);
            }

            return await _fallback.GetPolicyAsync(policyName);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
            _fallback.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
            _fallback.GetFallbackPolicyAsync();
    }
}
