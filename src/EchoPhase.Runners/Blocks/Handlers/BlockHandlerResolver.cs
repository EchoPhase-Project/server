// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    public class BlockHandlerResolver
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScope _scope;
        private readonly Dictionary<BlockTypes, Func<IBlockHandler>> _handlers = new();

        public BlockHandlerResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _scope = _serviceProvider.CreateScope();

            var handlerTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.GetCustomAttributes(typeof(BlockTypeHandlerAttribute), true).Any())
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBlockHandler<>)));

            foreach (var type in handlerTypes)
            {
                var attribute = type.GetCustomAttribute<BlockTypeHandlerAttribute>() ?? throw new InvalidOperationException($"Handler type '{type.FullName}' is missing required BlockTypeHandlerAttribute.");
                if (_handlers.ContainsKey(attribute.Type))
                    throw new InvalidOperationException($"Duplicate handler registration for type {attribute.Type}.");

                var ctor = type.GetConstructor(new Type[] { typeof(IServiceProvider) }) ?? throw new InvalidOperationException($"Handler '{type.FullName}' missing default constructor.");
                var factory = () =>
                    (IBlockHandler)ActivatorUtilities.CreateInstance(_scope.ServiceProvider, type);

                _handlers[attribute.Type] = factory;
            }
        }

        public IBlockHandler Get(BlockTypes type)
        {
            if (_handlers.TryGetValue(type, out var handler))
                return handler();

            throw new InvalidOperationException($"No handler for block type: {type}");
        }
    }
}
