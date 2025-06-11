using System.Reflection;

using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;

namespace EchoPhase.Runners.Handlers
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
                var attribute = type.GetCustomAttribute<BlockTypeHandlerAttribute>();
                if (attribute == null)
                    throw new InvalidOperationException($"Handler type '{type.FullName}' is missing required BlockTypeHandlerAttribute.");

                if (_handlers.ContainsKey(attribute.Type))
                    throw new InvalidOperationException($"Duplicate handler registration for type {attribute.Type}.");

                var ctor = type.GetConstructor(new Type[] { typeof(IServiceProvider) });
                if (ctor == null)
                    throw new InvalidOperationException($"Handler '{type.FullName}' missing default constructor.");

                var factory = () =>
                    (IBlockHandler)ActivatorUtilities.CreateInstance(_scope.ServiceProvider, type)!;

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
