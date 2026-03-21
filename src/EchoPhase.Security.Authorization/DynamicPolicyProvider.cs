// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.Authorization.Builders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Authorization
{
    public class DynamicPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;
        private readonly IReadOnlyDictionary<string, IPolicyBuilder> _builders;
        private readonly ConcurrentDictionary<string, AuthorizationPolicy?> _cache = new();

        public DynamicPolicyProvider(
            IOptions<AuthorizationOptions> options,
            IEnumerable<IPolicyBuilder> builders)
        {
            _fallback = new DefaultAuthorizationPolicyProvider(options);

            _builders = builders
                .Select(b =>
                {
                    var prefix = b.GetType()
                        .GetCustomAttributes(typeof(PolicyPrefixAttribute), inherit: false)
                        .OfType<PolicyPrefixAttribute>()
                        .FirstOrDefault()
                        ?.Prefix
                        ?? throw new InvalidOperationException(
                            $"'{b.GetType().Name}' is missing required [PolicyPrefix] attribute.");

                    return (Prefix: prefix, Builder: b);
                })
                .ToDictionary(x => x.Prefix, x => x.Builder, StringComparer.Ordinal);
        }

        public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            return _cache.GetOrAdd(policyName, name =>
            {
                foreach (var (prefix, builder) in _builders)
                {
                    if (!name.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    return builder.Build(name[prefix.Length..]);
                }

                return null;
            }) ?? await _fallback.GetPolicyAsync(policyName);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
            _fallback.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
            _fallback.GetFallbackPolicyAsync();
    }
}
