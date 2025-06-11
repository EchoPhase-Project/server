using System.Net.WebSockets;
using System.Reflection;
using EchoPhase.Attributes;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Handlers
{
    public class OpCodeHandlerResolver
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScope _scope;
        private readonly Dictionary<OpCodes, Func<IOpCodeHandler>> _handlers = new();

        public OpCodeHandlerResolver(
            IServiceProvider serviceProvider
        )
        {
            _serviceProvider = serviceProvider;
            _scope = _serviceProvider.CreateScope();

            var handlerTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.GetCustomAttributes(typeof(OpCodeHandlerAttribute), true).Any())
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IOpCodeHandler<>)));

            foreach (var type in handlerTypes)
            {
                var attribute = type.GetCustomAttribute<OpCodeHandlerAttribute>();
                if (attribute == null)
                    throw new InvalidOperationException($"Handler type '{type.FullName}' is missing required OpCodeHandlerAttribute.");

                if (_handlers.ContainsKey(attribute.OpCode))
                    throw new InvalidOperationException($"Duplicate handler registration for opcode {attribute.OpCode}.");

                var ctor = type.GetConstructor(new Type[] { typeof(IServiceProvider) });
                if (ctor == null)
                    throw new InvalidOperationException($"Handler '{type.FullName}' missing default constructor.");

                var factory = () =>
                    (IOpCodeHandler)ActivatorUtilities.CreateInstance(_scope.ServiceProvider, type)!;

                _handlers[attribute.OpCode] = factory;
            }
        }

        public IOpCodeHandler GetHandler(OpCodes opCode)
        {
            if (!opCode.IsValid())
                throw new NotSupportedException($"Unsupported OpCode {opCode} value.");

            if (opCode.IsIgnored())
                throw new NotSupportedException($"Ignored OpCode {opCode} value.");

            if (_handlers.TryGetValue(opCode, out var handler))
            {
                return handler();
            }

            throw new NotSupportedException($"No handler registered for OpCode {opCode}.");
        }

        public async Task HandleAsync(OpCodes opCode, WebSocket webSocket, object payload)
        {
            var handler = GetHandler(opCode);

            await handler.HandleAsync(webSocket, payload);
        }
    }
}
