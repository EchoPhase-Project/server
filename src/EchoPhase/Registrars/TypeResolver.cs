// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Spectre.Console.Cli;

namespace EchoPhase.Registrars
{
    public sealed class TypeResolver : ITypeResolver, IDisposable
    {
        private readonly IServiceScope _scope;

        public TypeResolver(IServiceProvider provider)
        {
            _scope = provider.CreateScope();
        }

        public object Resolve(Type? type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                var elementType = type.GetGenericArguments()[0];
                var method = typeof(ServiceProviderServiceExtensions)
                    .GetMethods()
                    .First(m => m.Name == "GetServices"
                             && m.IsGenericMethod
                             && m.GetParameters().Length == 1
                             && m.GetParameters()[0].ParameterType == typeof(IServiceProvider))
                    .MakeGenericMethod(elementType);

                return method.Invoke(null, new object[] { _scope.ServiceProvider })
                    ?? throw new InvalidOperationException($"Cannot resolve type {type.Name}.");
            }

            return ActivatorUtilities.CreateInstance(_scope.ServiceProvider, type);
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
