using Spectre.Console.Cli;

namespace EchoPhase.Registrars
{
    public sealed class TypeRegistrar : ITypeRegistrar
    {
        private readonly IServiceProvider _provider;

        public TypeRegistrar(IServiceProvider provider)
        {
            _provider = provider;
        }

        public ITypeResolver Build()
        {
            return new TypeResolver(_provider);
        }

        public void Register(Type service, Type implementation)
        {
        }

        public void RegisterInstance(Type service, object implementation)
        {
        }

        public void RegisterLazy(Type service, Func<object> factory)
        {
        }
    }
}
