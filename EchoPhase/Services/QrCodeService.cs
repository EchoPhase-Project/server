using System.Reflection;

using EchoPhase.Attributes;
using EchoPhase.Interfaces;

namespace EchoPhase.Services
{
    public class QrCodeService
    {
        private readonly Dictionary<string, Func<IQrCodeGenerator>> _generators = new();

        public QrCodeService()
        {
            var generatorTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.GetCustomAttributes(typeof(QrFormatAttribute), true).Any())
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQrCodeGenerator)));

            foreach (var type in generatorTypes)
            {
                var attribute = type.GetCustomAttribute<QrFormatAttribute>();
                if (attribute == null)
                    throw new InvalidOperationException($"Generator type '{type.FullName}' is missing required QrFormatAttribute.");

                if (_generators.ContainsKey(attribute.Format))
                    throw new InvalidOperationException($"Duplicate generator registration for format {attribute.Format}.");

                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null)
                    throw new InvalidOperationException($"Generator '{type.FullName}' missing default constructor.");

                var factory = () => (IQrCodeGenerator)Activator.CreateInstance(type)!;

                _generators[attribute.Format] = factory;
            }
        }

        public IQrCodeGenerator GetGenerator(string format)
        {
            if (_generators.TryGetValue(format.ToLowerInvariant(), out var factory))
            {
                return factory();
            }

            throw new InvalidOperationException($"No QR generator registered for format '{format}'.");
        }
    }
}
