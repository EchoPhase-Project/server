using System.Reflection;
using EchoPhase.QRCodes.Generators;

namespace EchoPhase.QRCodes
{
    public class QRCodeService
    {
        private readonly Dictionary<string, Func<IQRCodeGenerator>> _generators = new();

        public QRCodeService()
        {
            var generatorTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.GetCustomAttributes(typeof(QRFormatAttribute), true).Any())
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQRCodeGenerator)));

            foreach (var type in generatorTypes)
            {
                var attribute = type.GetCustomAttribute<QRFormatAttribute>();
                if (attribute == null)
                    throw new InvalidOperationException($"Generator type '{type.FullName}' is missing required QrFormatAttribute.");

                if (_generators.ContainsKey(attribute.Format))
                    throw new InvalidOperationException($"Duplicate generator registration for format {attribute.Format}.");

                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null)
                    throw new InvalidOperationException($"Generator '{type.FullName}' missing default constructor.");

                var factory = () =>
                {
                    var instance = Activator.CreateInstance(type) as IQRCodeGenerator;
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            $"Type {type.Name} does not implement IQRCodeGenerator or failed to instantiate");
                    }
                    return instance;
                };

                _generators[attribute.Format] = factory;
            }
        }

        public IQRCodeGenerator GetGenerator(string format)
        {
            if (_generators.TryGetValue(format.ToLowerInvariant(), out var factory))
            {
                return factory();
            }

            throw new InvalidOperationException($"No QR generator registered for format '{format}'.");
        }
    }
}
